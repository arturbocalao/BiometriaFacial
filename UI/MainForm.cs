using System.Drawing;
using BiometriaFacial.Biometria;
using BiometriaFacial.Data;
using BiometriaFacial.Servicos;
using Emgu.CV;

namespace BiometriaFacial.UI;

public class MainForm : Form
{
    private static readonly Color Verde = Color.FromArgb(198, 239, 206);
    private static readonly Color Vermelho = Color.FromArgb(255, 199, 206);
    private static readonly Color Amarelo = Color.FromArgb(255, 235, 156);
    private static readonly Color Cinza = Color.FromArgb(235, 238, 242);

    private readonly Banco _banco;
    private readonly Preprocessador _pre;
    private readonly BiometriaServico _bio;

    // --- cadastro ---
    private readonly List<Mat> _amostras = new();
    private readonly TextBox _txtNome = new() { Location = new Point(15, 40), Width = 340 };
    private readonly FlowLayoutPanel _flowAmostras = new() { Dock = DockStyle.Top, Height = 125, AutoScroll = true, WrapContents = false, BackColor = Cinza };
    private readonly Label _lblQtd = new() { Location = new Point(15, 230), Width = 340, Height = 24, Text = "Fotos válidas: 0" };
    private readonly DataGridView _gridPessoas = Grade();

    // --- identificação e autenticação ---
    private PainelLeitura _pId = null!;
    private PainelLeitura _pAu = null!;
    private readonly ComboBox _cmbPessoa = new() { Location = new Point(15, 40), Width = 350, DropDownStyle = ComboBoxStyle.DropDownList };

    // --- log e avaliação ---
    private readonly DataGridView _gridLog = Grade();
    private readonly TextBox _txtPasta = new() { Location = new Point(15, 40), Width = 640 };
    private readonly NumericUpDown _numTreino = new() { Location = new Point(215, 82), Width = 60, Minimum = 1, Maximum = 20, Value = 5 };
    private readonly Button _btnAvaliar = new() { Text = "Avaliar", Location = new Point(300, 76), Size = new Size(120, 36) };
    private readonly Button _btnExportarAval = new() { Text = "Exportar CSV...", Location = new Point(430, 76), Size = new Size(150, 36) };
    private readonly DataGridView _gridAval = Grade();
    private readonly Label _lblAval = new() { Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(12, 8, 8, 8), BackColor = Cinza, Text = "Escolha a pasta da base e clique em Avaliar." };
    private ResultadoAvaliacao? _ultimaAval;

    private readonly NumericUpDown _numLimiar = new() { Minimum = 10, Maximum = 300, Value = 80, Width = 70 };

    // Agrupa os controles que se repetem nas abas de identificação e autenticação.
    private class PainelLeitura
    {
        public Panel Raiz = new() { Dock = DockStyle.Fill };
        public Panel Lateral = new() { Dock = DockStyle.Right, Width = 385 };
        public PictureBox Imagem = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(25, 30, 40) };
        public PictureBox Rosto = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Size = new Size(150, 150) };
        public Label Resultado = new() { Size = new Size(355, 95), TextAlign = ContentAlignment.MiddleCenter, BackColor = Cinza, Font = new Font("Segoe UI", 15F, FontStyle.Bold), Text = "Aguardando imagem" };
        public Label Detalhe = new() { Size = new Size(355, 110) };
    }

    public MainForm(Banco banco, Preprocessador pre, BiometriaServico bio)
    {
        _banco = banco;
        _pre = pre;
        _bio = bio;

        Text = "Identificação e Autenticação Biométrica Facial";
        ClientSize = new Size(1180, 740);
        MinimumSize = new Size(1000, 680);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        _pId = CriarPainelLeitura(webcam => ExecutarIdentificacao(webcam), 0);
        _pAu = CriarPainelLeitura(webcam => ExecutarAutenticacao(webcam), 70);

        var abas = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10.5F) };
        abas.TabPages.Add(CriarAbaCadastro());
        abas.TabPages.Add(CriarAbaIdentificacao());
        abas.TabPages.Add(CriarAbaAutenticacao());
        abas.TabPages.Add(CriarAbaLog());
        abas.TabPages.Add(CriarAbaAvaliacao());

        Controls.Add(abas);
        Controls.Add(CriarTopo());

        _numLimiar.ValueChanged += (_, _) => _bio.Limiar = (double)_numLimiar.Value;
        Load += (_, _) => { AtualizarPessoas(); AtualizarLog(); };
        FormClosed += (_, _) => LimparAmostras();
    }

    // ===================== montagem das telas =====================

    private Panel CriarTopo()
    {
        var topo = new Panel { Dock = DockStyle.Top, Height = 56, BackColor = Color.FromArgb(30, 41, 59) };
        topo.Controls.Add(new Label
        {
            Text = "Sistema de Identificação e Autenticação Biométrica Facial",
            ForeColor = Color.White, Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            AutoSize = true, Location = new Point(15, 14)
        });

        var lblLimiar = new Label
        {
            Text = "Limiar de distância:", ForeColor = Color.White, AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Right, Location = new Point(ClientSize.Width - 250, 18)
        };
        _numLimiar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        _numLimiar.Location = new Point(ClientSize.Width - 95, 15);
        topo.Controls.Add(lblLimiar);
        topo.Controls.Add(_numLimiar);
        return topo;
    }

    private TabPage CriarAbaCadastro()
    {
        var aba = new TabPage("1. Cadastro");

        // lado esquerdo: formulário
        var esquerda = new Panel { Dock = DockStyle.Left, Width = 375 };
        esquerda.Controls.Add(Rotulo("Nome completo:", 15, 12));
        esquerda.Controls.Add(_txtNome);

        var btnArquivo = Botao("Adicionar fotos de arquivo...", 15, 80, 340);
        var btnWebcam = Botao("Adicionar foto pela webcam...", 15, 126, 340);
        var dica = Rotulo($"Use no mínimo {BiometriaServico.MinAmostras} fotos (o ideal é de 5 a 10), com pequenas variações de ângulo e iluminação.", 15, 175, 340, 50);
        var btnLimpar = Botao("Limpar fotos", 15, 262, 340);
        var btnSalvar = Botao("Salvar cadastro", 15, 316, 340, 46);
        btnSalvar.BackColor = Color.FromArgb(34, 139, 87);
        btnSalvar.ForeColor = Color.White;
        btnSalvar.Font = new Font("Segoe UI", 11F, FontStyle.Bold);

        btnArquivo.Click += (_, _) => AdicionarFotosDeArquivo();
        btnWebcam.Click += (_, _) => AdicionarFotoDaWebcam();
        btnLimpar.Click += (_, _) => LimparAmostras();
        btnSalvar.Click += (_, _) => SalvarCadastro();

        esquerda.Controls.AddRange(new Control[] { btnArquivo, btnWebcam, dica, _lblQtd, btnLimpar, btnSalvar });

        // lado direito: miniaturas e lista de pessoas
        var direita = new Panel { Dock = DockStyle.Fill };
        var btnVer = Botao("Ver fotos da pessoa selecionada", 10, 8, 290);
        var btnExcluir = Botao("Excluir pessoa selecionada", 310, 8, 260);
        var rodape = new Panel { Dock = DockStyle.Bottom, Height = 54 };
        rodape.Controls.Add(btnVer);
        rodape.Controls.Add(btnExcluir);
        btnVer.Click += (_, _) => VerFotosPessoaSelecionada();
        btnExcluir.Click += (_, _) => ExcluirPessoaSelecionada();

        var tituloMini = new Label { Dock = DockStyle.Top, Height = 28, Text = "Rostos detectados nas fotos (recortados):", Padding = new Padding(4, 4, 0, 0) };

        direita.Controls.Add(_gridPessoas);   // Fill primeiro
        direita.Controls.Add(rodape);
        direita.Controls.Add(_flowAmostras);
        direita.Controls.Add(tituloMini);

        aba.Controls.Add(direita);
        aba.Controls.Add(esquerda);
        return aba;
    }

    private TabPage CriarAbaIdentificacao()
    {
        var aba = new TabPage("2. Identificação (1:N)");
        aba.Controls.Add(_pId.Raiz);
        return aba;
    }

    private TabPage CriarAbaAutenticacao()
    {
        var aba = new TabPage("3. Autenticação (1:1)");
        _pAu.Lateral.Controls.Add(Rotulo("Quem a pessoa diz ser:", 15, 12));
        _pAu.Lateral.Controls.Add(_cmbPessoa);
        aba.Controls.Add(_pAu.Raiz);
        return aba;
    }

    private TabPage CriarAbaLog()
    {
        var aba = new TabPage("4. Registro de acessos");
        var topo = new Panel { Dock = DockStyle.Top, Height = 54 };
        var btnAtualizar = Botao("Atualizar", 10, 8, 130);
        var btnCsv = Botao("Exportar CSV...", 150, 8, 160);
        btnAtualizar.Click += (_, _) => AtualizarLog();
        btnCsv.Click += (_, _) => ExportarLog();
        topo.Controls.Add(btnAtualizar);
        topo.Controls.Add(btnCsv);

        aba.Controls.Add(_gridLog);
        aba.Controls.Add(topo);
        return aba;
    }

    private TabPage CriarAbaAvaliacao()
    {
        var aba = new TabPage("5. Avaliação (FAR/FRR)");
        var topo = new Panel { Dock = DockStyle.Top, Height = 125 };
        topo.Controls.Add(Rotulo("Pasta da base de imagens (uma subpasta por pessoa):", 15, 12, 500));
        topo.Controls.Add(_txtPasta);
        var btnProcurar = Botao("Procurar...", 665, 34, 120, 32);
        topo.Controls.Add(btnProcurar);
        topo.Controls.Add(Rotulo("Fotos de treino por pessoa:", 15, 85, 200));
        topo.Controls.Add(_numTreino);
        topo.Controls.Add(_btnAvaliar);
        topo.Controls.Add(_btnExportarAval);

        btnProcurar.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Selecione a pasta que contém as subpastas das pessoas" };
            if (dlg.ShowDialog(this) == DialogResult.OK) _txtPasta.Text = dlg.SelectedPath;
        };
        _btnAvaliar.Click += (_, _) => Avaliar();
        _btnExportarAval.Click += (_, _) => ExportarAvaliacao();

        aba.Controls.Add(_gridAval);
        aba.Controls.Add(_lblAval);
        aba.Controls.Add(topo);
        return aba;
    }

    private PainelLeitura CriarPainelLeitura(Action<bool> acao, int deslocamento)
    {
        var p = new PainelLeitura();
        var btnArquivo = Botao("Abrir imagem de arquivo...", 15, 15 + deslocamento, 355, 42);
        var btnWebcam = Botao("Usar a webcam...", 15, 63 + deslocamento, 355, 42);
        btnArquivo.Click += (_, _) => acao(false);
        btnWebcam.Click += (_, _) => acao(true);

        p.Resultado.Location = new Point(15, 125 + deslocamento);
        p.Detalhe.Location = new Point(15, 230 + deslocamento);
        var lblRosto = Rotulo("Rosto após o pré-processamento:", 15, 345 + deslocamento, 355);
        p.Rosto.Location = new Point(15, 372 + deslocamento);

        p.Lateral.Controls.AddRange(new Control[] { btnArquivo, btnWebcam, p.Resultado, p.Detalhe, lblRosto, p.Rosto });
        p.Raiz.Controls.Add(p.Imagem);     // Fill primeiro
        p.Raiz.Controls.Add(p.Lateral);
        return p;
    }

    // ===================== cadastro =====================

    private void AdicionarFotosDeArquivo()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Selecione as fotos da pessoa",
            Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.pgm|Todos os arquivos|*.*",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var erros = new List<string>();
        foreach (string arquivo in dlg.FileNames)
        {
            try
            {
                Mat imagem = ImagemUtil.Carregar(arquivo);
                string? erro = TentarAdicionarAmostra(imagem);
                if (erro != null) erros.Add($"{Path.GetFileName(arquivo)}: {erro}");
            }
            catch (BiometriaException ex)
            {
                erros.Add($"{Path.GetFileName(arquivo)}: {ex.Message}");
            }
        }

        if (erros.Count > 0)
            Aviso("Algumas fotos não foram aceitas:\n\n" + string.Join("\n\n", erros));
    }

    private void AdicionarFotoDaWebcam()
    {
        Mat? imagem = ObterImagem(true);
        if (imagem == null) return;

        string? erro = TentarAdicionarAmostra(imagem);
        if (erro != null) Aviso(erro);
    }

    // Valida a foto (precisa ter exatamente um rosto). Se der certo, a lista passa a ser dona do Mat.
    private string? TentarAdicionarAmostra(Mat imagem)
    {
        try
        {
            var (cinza, cor) = _pre.ExtrairRostoComCor(imagem);
            using Mat rostoCinza = cinza;
            using Mat rostoCor = cor;
            _amostras.Add(imagem);

            _flowAmostras.Controls.Add(new PictureBox
            {
                Size = new Size(100, 100), SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(6),
                Image = ImagemUtil.ParaBitmap(rostoCor)
            });
            _lblQtd.Text = $"Fotos válidas: {_amostras.Count}";
            return null;
        }
        catch (BiometriaException ex)
        {
            imagem.Dispose();
            return ex.Message;
        }
    }

    private void LimparAmostras()
    {
        foreach (Mat m in _amostras) m.Dispose();
        _amostras.Clear();

        // Primeiro solta a imagem de cada miniatura e tira da tela; só depois descarta.
        // (descartar a imagem enquanto o PictureBox ainda a desenha causa "Parameter is not valid")
        var miniaturas = _flowAmostras.Controls.OfType<PictureBox>().ToList();
        _flowAmostras.Controls.Clear();
        foreach (PictureBox pb in miniaturas)
        {
            Image? img = pb.Image;
            pb.Image = null;
            img?.Dispose();
            pb.Dispose();
        }
        _lblQtd.Text = "Fotos válidas: 0";
    }

    private void SalvarCadastro()
    {
        try
        {
            _bio.Cadastrar(_txtNome.Text, _amostras);
            Info($"{_txtNome.Text.Trim()} foi cadastrado(a) com {_amostras.Count} fotos.");
            _txtNome.Clear();
            LimparAmostras();
            AtualizarPessoas();
        }
        catch (BiometriaException ex)
        {
            Aviso(ex.Message);
        }
        catch (Exception ex)
        {
            Aviso("Erro inesperado ao salvar: " + ex.Message);
        }
    }

    // Abre uma janela com os rostos (já recortados) guardados para a pessoa selecionada.
    private void VerFotosPessoaSelecionada()
    {
        if (_gridPessoas.CurrentRow?.DataBoundItem is not Pessoa pessoa)
        {
            Aviso("Selecione uma pessoa na lista.");
            return;
        }

        var caminhos = _banco.ListarAmostras()
            .Where(a => a.PessoaId == pessoa.Id)
            .Select(a => a.Caminho)
            .ToList();
        if (caminhos.Count == 0)
        {
            Aviso("Esta pessoa não tem fotos salvas.");
            return;
        }

        using var janela = new Form
        {
            Text = $"Fotos de {pessoa.Nome} ({caminhos.Count})",
            ClientSize = new Size(760, 480),
            StartPosition = FormStartPosition.CenterParent
        };
        var painel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(10) };
        janela.Controls.Add(painel);

        foreach (string caminho in caminhos)
        {
            try
            {
                // mostra a versão colorida, se existir (cadastros antigos só têm a cinza)
                string arquivoCor = Banco.CaminhoCor(caminho);
                using Mat rosto = ImagemUtil.Carregar(File.Exists(arquivoCor) ? arquivoCor : caminho);
                painel.Controls.Add(new PictureBox
                {
                    Size = new Size(140, 140), SizeMode = PictureBoxSizeMode.Zoom,
                    BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(8),
                    Image = ImagemUtil.ParaBitmap(rosto)
                });
            }
            catch (BiometriaException)
            {
                // arquivo apagado ou ilegível: ignora essa foto
            }
        }

        janela.ShowDialog(this);

        foreach (PictureBox pb in painel.Controls.OfType<PictureBox>())
        {
            Image? img = pb.Image;
            pb.Image = null;
            img?.Dispose();
        }
    }

    private void ExcluirPessoaSelecionada()
    {
        if (_gridPessoas.CurrentRow?.DataBoundItem is not Pessoa pessoa)
        {
            Aviso("Selecione uma pessoa na lista.");
            return;
        }
        var resp = MessageBox.Show($"Excluir {pessoa.Nome} e todas as fotos dela?", "Confirmar exclusão",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (resp != DialogResult.Yes) return;

        _bio.ExcluirPessoa(pessoa.Id);
        AtualizarPessoas();
    }

    private void AtualizarPessoas()
    {
        List<Pessoa> pessoas = _bio.ListarPessoas();
        _gridPessoas.DataSource = pessoas;
        Cabecalhos(_gridPessoas, ("Id", "Código"), ("Nome", "Nome"), ("QtdAmostras", "Fotos"), ("CriadoEm", "Cadastrado em"));

        _cmbPessoa.DataSource = null;
        _cmbPessoa.DisplayMember = "Nome";
        _cmbPessoa.ValueMember = "Id";
        _cmbPessoa.DataSource = pessoas.ToList();
    }

    // ===================== identificação e autenticação =====================

    private void ExecutarIdentificacao(bool webcam)
    {
        using Mat? imagem = ObterImagem(webcam);
        if (imagem == null) return;

        ExibirImagem(_pId.Imagem, imagem);
        try
        {
            ResultadoBiometrico r = _bio.Identificar(imagem, webcam ? "webcam" : "arquivo");
            MostrarResultado(_pId, r, r.Aceito ? $"IDENTIFICADO\n{r.Nome}" : "NÃO RECONHECIDO");
        }
        catch (BiometriaException ex) { MostrarErro(_pId, ex.Message); }
        catch (Exception ex) { MostrarErro(_pId, "Erro inesperado: " + ex.Message); }
        AtualizarLog();
    }

    private void ExecutarAutenticacao(bool webcam)
    {
        if (_cmbPessoa.SelectedItem is not Pessoa pessoa)
        {
            Aviso("Cadastre pelo menos uma pessoa e selecione quem está tentando entrar.");
            return;
        }

        using Mat? imagem = ObterImagem(webcam);
        if (imagem == null) return;

        ExibirImagem(_pAu.Imagem, imagem);
        try
        {
            ResultadoBiometrico r = _bio.Autenticar(pessoa.Id, imagem, webcam ? "webcam" : "arquivo");
            MostrarResultado(_pAu, r, r.Aceito ? $"ACESSO LIBERADO\n{pessoa.Nome}" : "ACESSO NEGADO");
        }
        catch (BiometriaException ex) { MostrarErro(_pAu, ex.Message); }
        catch (Exception ex) { MostrarErro(_pAu, "Erro inesperado: " + ex.Message); }
        AtualizarLog();
    }

    private Mat? ObterImagem(bool webcam)
    {
        try
        {
            if (webcam)
            {
                using var janela = new CapturaForm(_pre);
                return janela.ShowDialog(this) == DialogResult.OK ? janela.Resultado : null;
            }

            using var dlg = new OpenFileDialog
            {
                Title = "Selecione uma imagem",
                Filter = "Imagens|*.jpg;*.jpeg;*.png;*.bmp;*.pgm|Todos os arquivos|*.*"
            };
            if (dlg.ShowDialog(this) != DialogResult.OK) return null;
            return ImagemUtil.Carregar(dlg.FileName);
        }
        catch (BiometriaException ex)
        {
            Aviso(ex.Message);
            return null;
        }
    }

    private void ExibirImagem(PictureBox destino, Mat imagem)
    {
        using Mat marcada = _pre.DesenharRostos(imagem);
        Image? antiga = destino.Image;
        destino.Image = ImagemUtil.ParaBitmap(marcada);
        antiga?.Dispose();
    }

    private void MostrarResultado(PainelLeitura p, ResultadoBiometrico r, string titulo)
    {
        p.Resultado.Text = titulo;
        p.Resultado.Font = new Font("Segoe UI", 15F, FontStyle.Bold);
        p.Resultado.BackColor = r.Aceito ? Verde : Vermelho;
        p.Detalhe.Text = $"Distância LBPH: {r.Distancia:F2}\n" +
                         $"Similaridade: {r.Similaridade:F1}%\n" +
                         $"Limiar atual: {_bio.Limiar:F0}\n\n{r.Mensagem}";
        Image? antiga = p.Rosto.Image;
        p.Rosto.Image = r.Rosto;
        antiga?.Dispose();
    }

    private static void MostrarErro(PainelLeitura p, string mensagem)
    {
        p.Resultado.Text = "NÃO FOI POSSÍVEL ANALISAR";
        p.Resultado.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        p.Resultado.BackColor = Amarelo;
        p.Detalhe.Text = mensagem;
        Image? antiga = p.Rosto.Image;
        p.Rosto.Image = null;
        antiga?.Dispose();
    }

    // ===================== log =====================

    private void AtualizarLog()
    {
        _gridLog.DataSource = _banco.ListarAcessos();
        Cabecalhos(_gridLog, ("Id", "Nº"), ("DataHora", "Data/hora"), ("Tipo", "Operação"), ("Nome", "Pessoa"),
                   ("Distancia", "Distância"), ("Resultado", "Resultado"), ("Origem", "Origem"));
    }

    private void ExportarLog()
    {
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "acessos.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var linhas = _banco.ListarAcessos(100000).Select(a => (IEnumerable<string>)new[]
            {
                a.Id.ToString(), a.DataHora, a.Tipo, a.Nome, a.Distancia.ToString("F2"), a.Resultado, a.Origem
            });
            CsvUtil.Salvar(dlg.FileName, new[] { "Nº", "Data/hora", "Operação", "Pessoa", "Distância", "Resultado", "Origem" }, linhas);
            Info("Arquivo salvo em:\n" + dlg.FileName);
        }
        catch (IOException ex)
        {
            Aviso("Não foi possível salvar o arquivo (ele pode estar aberto em outro programa).\n\n" + ex.Message);
        }
    }

    // ===================== avaliação =====================

    private async void Avaliar()
    {
        string pasta = _txtPasta.Text.Trim();
        int treino = (int)_numTreino.Value;

        _btnAvaliar.Enabled = false;
        _lblAval.Text = "Avaliando, aguarde...";
        try
        {
            ResultadoAvaliacao r = await Task.Run(() => Avaliador.Avaliar(pasta, treino));
            _ultimaAval = r;
            _gridAval.DataSource = r.Linhas;
            Cabecalhos(_gridAval, ("Limiar", "Limiar"), ("FAR", "FAR (%)"), ("FRR", "FRR (%)"),
                       ("ImpostoresAceitos", "Impostores aceitos"), ("GenuinosRejeitados", "Genuínos rejeitados"));

            _lblAval.Text =
                $"Pessoas cadastradas: {r.PessoasCadastradas}  |  Pessoas impostoras: {r.PessoasImpostoras}  |  " +
                $"Testes genuínos: {r.TestesGenuinos}  |  Testes de impostores: {r.TestesImpostores}\n" +
                $"Acerto na identificação (sem limiar): {r.AcuraciaRank1:F2}%   |   EER aproximado: {r.EER:F2}%   |   Limiar sugerido: {r.LimiarSugerido:F0}";
        }
        catch (BiometriaException ex)
        {
            _lblAval.Text = "Não foi possível avaliar.";
            Aviso(ex.Message);
        }
        catch (Exception ex)
        {
            _lblAval.Text = "Não foi possível avaliar.";
            Aviso("Erro inesperado na avaliação: " + ex.Message);
        }
        finally
        {
            _btnAvaliar.Enabled = true;
        }
    }

    private void ExportarAvaliacao()
    {
        if (_ultimaAval == null)
        {
            Aviso("Execute a avaliação antes de exportar.");
            return;
        }
        using var dlg = new SaveFileDialog { Filter = "CSV|*.csv", FileName = "avaliacao_far_frr.csv" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            var linhas = _ultimaAval.Linhas.Select(l => (IEnumerable<string>)new[]
            {
                l.Limiar.ToString("F0"), l.FAR.ToString("F2"), l.FRR.ToString("F2"),
                l.ImpostoresAceitos.ToString(), l.GenuinosRejeitados.ToString()
            });
            CsvUtil.Salvar(dlg.FileName, new[] { "Limiar", "FAR (%)", "FRR (%)", "Impostores aceitos", "Genuínos rejeitados" }, linhas);
            Info("Arquivo salvo em:\n" + dlg.FileName);
        }
        catch (IOException ex)
        {
            Aviso("Não foi possível salvar o arquivo (ele pode estar aberto em outro programa).\n\n" + ex.Message);
        }
    }

    // ===================== utilidades de interface =====================

    private static Button Botao(string texto, int x, int y, int largura, int altura = 38) => new()
    {
        Text = texto, Location = new Point(x, y), Size = new Size(largura, altura)
    };

    private static Label Rotulo(string texto, int x, int y, int largura = 300, int altura = 24) => new()
    {
        Text = texto, Location = new Point(x, y), Size = new Size(largura, altura)
    };

    private static DataGridView Grade() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, BackgroundColor = Color.White,
        RowHeadersVisible = false
    };

    private static void Cabecalhos(DataGridView grade, params (string Coluna, string Texto)[] titulos)
    {
        foreach (var (coluna, texto) in titulos)
            if (grade.Columns[coluna] != null) grade.Columns[coluna]!.HeaderText = texto;
    }

    private void Aviso(string mensagem) =>
        MessageBox.Show(this, mensagem, "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);

    private void Info(string mensagem) =>
        MessageBox.Show(this, mensagem, "Informação", MessageBoxButtons.OK, MessageBoxIcon.Information);
}
