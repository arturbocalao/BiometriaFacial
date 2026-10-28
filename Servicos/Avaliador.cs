using BiometriaFacial.Biometria;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace BiometriaFacial.Servicos;

public record LinhaAvaliacao(double Limiar, double FAR, double FRR, int ImpostoresAceitos, int GenuinosRejeitados);

public record ResultadoAvaliacao(
    int PessoasCadastradas, int PessoasImpostoras, int TestesGenuinos, int TestesImpostores,
    double AcuraciaRank1, double EER, double LimiarSugerido, List<LinhaAvaliacao> Linhas);

// Mede o desempenho do reconhecedor em uma base de imagens organizada em subpastas
// (uma pasta por pessoa, ex.: a base ORL/AT&T com s1..s40).
//  - As primeiras N fotos de cada pessoa "cadastrada" treinam o modelo.
//  - As fotos restantes dessas pessoas são os testes GENUÍNOS.
//  - Uma pessoa a cada cinco NÃO é cadastrada: suas fotos são os testes de IMPOSTORES.
// FAR = impostores aceitos / impostores testados.  FRR = genuínos rejeitados / genuínos testados.
public static class Avaliador
{
    private static readonly string[] Extensoes = { ".pgm", ".png", ".jpg", ".jpeg", ".bmp" };

    public static ResultadoAvaliacao Avaliar(string pasta, int fotosTreino)
    {
        if (!Directory.Exists(pasta))
            throw new BiometriaException("A pasta informada não existe.");

        var pessoas = new List<List<string>>();
        foreach (string dir in Directory.GetDirectories(pasta).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var arquivos = Directory.GetFiles(dir)
                .Where(f => Extensoes.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f.Length).ThenBy(f => f)       // 1,2,...,10 na ordem certa
                .ToList();
            if (arquivos.Count > 0) pessoas.Add(arquivos);
        }
        if (pessoas.Count < 5)
            throw new BiometriaException("A pasta precisa ter pelo menos 5 subpastas, uma por pessoa, com as fotos dentro.");

        var treino = new List<(int PessoaId, Mat Rosto)>();
        var genuinos = new List<(int PessoaId, Mat Rosto)>();
        var impostores = new List<Mat>();
        var todas = new List<Mat>();   // para liberar a memória no final

        try
        {
            int rotulo = 0, qtdImpostoras = 0;
            for (int i = 0; i < pessoas.Count; i++)
            {
                bool impostora = i % 5 == 4;
                if (impostora) qtdImpostoras++;

                for (int k = 0; k < pessoas[i].Count; k++)
                {
                    Mat rosto = CarregarRostoRecortado(pessoas[i][k]);
                    todas.Add(rosto);

                    if (impostora) impostores.Add(rosto);
                    else if (k < fotosTreino) treino.Add((rotulo, rosto));
                    else genuinos.Add((rotulo, rosto));
                }
                if (!impostora) rotulo++;
            }

            if (genuinos.Count == 0)
                throw new BiometriaException("Não sobraram fotos para teste. Diminua a quantidade de fotos de treino.");

            using var reconhecedor = new ReconhecedorFacial();
            reconhecedor.Treinar(treino);

            var distGenuinos = genuinos.Select(g => reconhecedor.Prever(g.Rosto)).ToList();
            var distImpostores = impostores.Select(m => reconhecedor.Prever(m)).ToList();

            int acertos = 0;
            for (int i = 0; i < genuinos.Count; i++)
                if (distGenuinos[i].PessoaId == genuinos[i].PessoaId) acertos++;

            var linhas = new List<LinhaAvaliacao>();
            for (double limiar = 30; limiar <= 150; limiar += 10)
            {
                int rejeitados = 0;
                for (int i = 0; i < genuinos.Count; i++)
                {
                    bool ok = distGenuinos[i].PessoaId == genuinos[i].PessoaId && distGenuinos[i].Distancia <= limiar;
                    if (!ok) rejeitados++;
                }
                int aceitos = distImpostores.Count(d => d.Distancia <= limiar);

                linhas.Add(new LinhaAvaliacao(
                    limiar,
                    Math.Round(100.0 * aceitos / impostores.Count, 2),
                    Math.Round(100.0 * rejeitados / genuinos.Count, 2),
                    aceitos, rejeitados));
            }

            // EER: ponto onde FAR e FRR ficam mais próximos
            var melhor = linhas.OrderBy(l => Math.Abs(l.FAR - l.FRR)).First();

            return new ResultadoAvaliacao(
                rotulo, qtdImpostoras, genuinos.Count, impostores.Count,
                Math.Round(100.0 * acertos / genuinos.Count, 2),
                Math.Round((melhor.FAR + melhor.FRR) / 2, 2),
                melhor.Limiar, linhas);
        }
        finally
        {
            foreach (var m in todas) m.Dispose();
        }
    }

    // Na base ORL as fotos já são só o rosto, então não usamos o detector aqui:
    // só passamos para cinza, 100x100 e equalizamos.
    private static Mat CarregarRostoRecortado(string caminho)
    {
        using var bruta = ImagemUtil.Carregar(caminho, ImreadModes.Grayscale);
        return Preprocessador.Normalizar(bruta);
    }
}
