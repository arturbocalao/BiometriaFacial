using System.Drawing;
using BiometriaFacial.Biometria;
using BiometriaFacial.Data;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace BiometriaFacial.Servicos;

public record ResultadoBiometrico(bool Aceito, string? Nome, double Distancia, string Mensagem, Bitmap? Rosto)
{
    // Só um indicador visual (0 a 100). Não é uma probabilidade.
    public double Similaridade => Math.Max(0, 100 - Distancia);
}

// Regras de negócio: cadastro, identificação (1:N) e autenticação (1:1).
public class BiometriaServico : IDisposable
{
    public const int MinAmostras = 3;

    private readonly Banco _banco;
    private readonly Preprocessador _pre;
    private readonly ReconhecedorFacial _reconhecedor = new();

    // Distância máxima aceita. Acima disso o rosto é considerado desconhecido.
    public double Limiar { get; set; } = 80;

    public BiometriaServico(Banco banco, Preprocessador pre)
    {
        _banco = banco;
        _pre = pre;
        Retreinar();
    }

    // Lê todas as amostras gravadas e treina o LBPH de novo.
    public void Retreinar()
    {
        var amostras = new List<(int PessoaId, Mat Rosto)>();
        try
        {
            foreach (var (pessoaId, caminho) in _banco.ListarAmostras())
            {
                if (!File.Exists(caminho)) continue;
                amostras.Add((pessoaId, ImagemUtil.Carregar(caminho, ImreadModes.Grayscale)));
            }
            _reconhecedor.Treinar(amostras);
        }
        finally
        {
            foreach (var (_, rosto) in amostras) rosto.Dispose();
        }
    }

    public List<Pessoa> ListarPessoas() => _banco.ListarPessoas();

    public void ExcluirPessoa(int pessoaId)
    {
        _banco.ExcluirPessoa(pessoaId);
        Retreinar();
    }

    // ---------- cadastro ----------

    public int Cadastrar(string nome, IList<Mat> imagens)
    {
        nome = (nome ?? "").Trim();
        if (nome.Length < 3)
            throw new BiometriaException("Informe o nome completo (mínimo de 3 letras).");
        if (_banco.NomeExiste(nome))
            throw new BiometriaException("Já existe uma pessoa cadastrada com esse nome.");
        if (imagens.Count < MinAmostras)
            throw new BiometriaException($"Adicione pelo menos {MinAmostras} fotos válidas para o cadastro.");

        // Primeiro extrai todos os rostos. Se alguma foto der erro, nada é gravado.
        var rostos = new List<Mat>();   // cinza 100x100 (reconhecimento)
        var cores = new List<Mat>();    // colorido (só exibição)
        try
        {
            for (int i = 0; i < imagens.Count; i++)
            {
                try
                {
                    var (cinza, cor) = _pre.ExtrairRostoComCor(imagens[i]);
                    rostos.Add(cinza);
                    cores.Add(cor);
                }
                catch (BiometriaException ex) { throw new BiometriaException($"Foto {i + 1}: {ex.Message}"); }
            }

            int pessoaId = _banco.InserirPessoa(nome);
            for (int i = 0; i < rostos.Count; i++)
            {
                string caminho = Path.Combine(_banco.PastaFaces, $"{pessoaId}_{i + 1}.png");
                ImagemUtil.SalvarPng(rostos[i], caminho);
                ImagemUtil.SalvarPng(cores[i], Banco.CaminhoCor(caminho));
                _banco.InserirAmostra(pessoaId, caminho);
            }

            Retreinar();
            return pessoaId;
        }
        finally
        {
            foreach (var r in rostos) r.Dispose();
            foreach (var c in cores) c.Dispose();
        }
    }

    // ---------- identificação 1:N ----------

    public ResultadoBiometrico Identificar(Mat imagem, string origem)
    {
        try
        {
            using Mat rosto = _pre.ExtrairRosto(imagem);
            var (pessoaId, distancia) = _reconhecedor.Prever(rosto);
            string? nome = _banco.ObterNome(pessoaId);

            bool aceito = nome != null && distancia <= Limiar;
            string msg = aceito ? "Pessoa identificada." : "Pessoa não reconhecida (distância acima do limiar).";

            _banco.RegistrarAcesso("IDENTIFICACAO", aceito ? nome! : "(desconhecido)",
                distancia, aceito ? "ACEITO" : "NEGADO", origem);

            return new ResultadoBiometrico(aceito, aceito ? nome : null, distancia, msg, ImagemUtil.ParaBitmap(rosto));
        }
        catch (BiometriaException)
        {
            _banco.RegistrarAcesso("IDENTIFICACAO", "-", 0, "ERRO", origem);
            throw;
        }
    }

    // ---------- autenticação 1:1 ----------

    public ResultadoBiometrico Autenticar(int pessoaIdInformada, Mat imagem, string origem)
    {
        string nomeInformado = _banco.ObterNome(pessoaIdInformada) ?? "?";
        try
        {
            using Mat rosto = _pre.ExtrairRosto(imagem);
            var (pessoaId, distancia) = _reconhecedor.Prever(rosto);

            // precisa ser a mesma pessoa informada E estar abaixo do limiar
            bool aceito = pessoaId == pessoaIdInformada && distancia <= Limiar;
            string msg = aceito
                ? "Identidade confirmada."
                : "Identidade NÃO confirmada: o rosto não corresponde à pessoa informada.";

            _banco.RegistrarAcesso("AUTENTICACAO", nomeInformado, distancia, aceito ? "ACEITO" : "NEGADO", origem);

            return new ResultadoBiometrico(aceito, nomeInformado, distancia, msg, ImagemUtil.ParaBitmap(rosto));
        }
        catch (BiometriaException)
        {
            _banco.RegistrarAcesso("AUTENTICACAO", nomeInformado, 0, "ERRO", origem);
            throw;
        }
    }

    public void Dispose() => _reconhecedor.Dispose();
}
