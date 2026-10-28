using System.Drawing;
using BiometriaFacial.Biometria;
using Emgu.CV;

namespace BiometriaFacial.UI;

// Janela da webcam: mostra a imagem ao vivo com o retângulo do rosto detectado
// e devolve o quadro capturado em "Resultado".
public class CapturaForm : Form
{
    private readonly Preprocessador _pre;
    private VideoCapture? _camera;
    private readonly PictureBox _tela = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    private readonly Button _btnCapturar = new() { Text = "Capturar foto", Dock = DockStyle.Bottom, Height = 48, Font = new Font("Segoe UI", 11F, FontStyle.Bold) };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 40 };
    private Mat? _ultimoQuadro;

    public Mat? Resultado { get; private set; }

    public CapturaForm(Preprocessador pre)
    {
        _pre = pre;

        Text = "Webcam";
        ClientSize = new Size(720, 580);
        StartPosition = FormStartPosition.CenterParent;

        Controls.Add(_tela);
        Controls.Add(_btnCapturar);

        _btnCapturar.Click += (_, _) => Capturar();
        _timer.Tick += (_, _) => AtualizarQuadro();
        Shown += (_, _) => IniciarCamera();
        FormClosed += (_, _) => Encerrar();
    }

    private void IniciarCamera()
    {
        try
        {
            _camera = new VideoCapture(0, VideoCapture.API.DShow);
            if (!_camera.IsOpened)
                throw new InvalidOperationException("A webcam não abriu.");
            _timer.Start();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível acessar a webcam.\n\n" + ex.Message +
                "\n\nVerifique se ela está conectada e se outro programa não está usando. " +
                "Você também pode usar a opção de abrir uma imagem de arquivo.",
                "Webcam indisponível", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            DialogResult = DialogResult.Cancel;
            Close();
        }
    }

    private void AtualizarQuadro()
    {
        if (_camera == null) return;

        using var quadro = new Mat();
        if (!_camera.Read(quadro) || quadro.IsEmpty) return;

        _ultimoQuadro?.Dispose();
        _ultimoQuadro = quadro.Clone();

        using var marcado = _pre.DesenharRostos(quadro);
        var antigo = _tela.Image;
        _tela.Image = ImagemUtil.ParaBitmap(marcado);
        antigo?.Dispose();
    }

    private void Capturar()
    {
        if (_ultimoQuadro == null || _ultimoQuadro.IsEmpty)
        {
            MessageBox.Show("Ainda não chegou nenhuma imagem da webcam. Aguarde um instante e tente de novo.",
                "Aguarde", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Resultado = _ultimoQuadro.Clone();
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Encerrar()
    {
        _timer.Stop();
        _camera?.Dispose();
        _camera = null;
        _ultimoQuadro?.Dispose();
        Image? imagem = _tela.Image;
        _tela.Image = null;
        imagem?.Dispose();
    }
}
