using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace BiometriaFacial.Biometria;

// Etapa de pré-processamento: detecta o rosto (Haar Cascade) e deixa a imagem
// no formato que o reconhecedor espera (cinza, 100x100, histograma equalizado).
public class Preprocessador : IDisposable
{
    public const int Lado = 100;

    private readonly CascadeClassifier _cascata;

    public Preprocessador(string caminhoCascata)
    {
        if (!File.Exists(caminhoCascata))
            throw new BiometriaException("Arquivo do detector facial não encontrado:\n" + caminhoCascata);
        _cascata = new CascadeClassifier(caminhoCascata);
    }

    public static Mat ParaCinza(Mat imagem)
    {
        if (imagem.NumberOfChannels == 1)
            return imagem.Clone();

        var cinza = new Mat();
        CvInvoke.CvtColor(imagem, cinza, ColorConversion.Bgr2Gray);
        return cinza;
    }

    // Redimensiona para 100x100 e equaliza o histograma (reduz o efeito da iluminação).
    public static Mat Normalizar(Mat cinza)
    {
        using var redimensionada = new Mat();
        CvInvoke.Resize(cinza, redimensionada, new Size(Lado, Lado), 0, 0, Inter.Linear);

        var equalizada = new Mat();
        CvInvoke.EqualizeHist(redimensionada, equalizada);
        return equalizada;
    }

    public Rectangle[] DetectarRostos(Mat imagem)
    {
        using var cinza = ParaCinza(imagem);
        return _cascata.DetectMultiScale(cinza, 1.1, 5, new Size(60, 60));
    }

    // Devolve o rosto recortado e normalizado, ou lança erro se não houver
    // exatamente um rosto na imagem.
    public Mat ExtrairRosto(Mat imagem)
    {
        var (cinza, cor) = ExtrairRostoComCor(imagem);
        cor.Dispose();
        return cinza;
    }

    // Devolve dois recortes do mesmo rosto:
    //  - Cinza: 100x100, equalizado, é o que o reconhecedor usa;
    //  - Cor: 200x200 colorido, usado só para mostrar na tela.
    public (Mat Cinza, Mat Cor) ExtrairRostoComCor(Mat imagem)
    {
        using var cinza = ParaCinza(imagem);
        Rectangle[] rostos = _cascata.DetectMultiScale(cinza, 1.1, 5, new Size(60, 60));

        if (rostos.Length == 0) throw new RostoNaoEncontradoException();
        if (rostos.Length > 1) throw new VariosRostosException(rostos.Length);

        using var recorteCinza = new Mat(cinza, rostos[0]);
        Mat normalizado = Normalizar(recorteCinza);

        using var recorteCor = new Mat(imagem, rostos[0]);
        var cor = new Mat();
        CvInvoke.Resize(recorteCor, cor, new Size(200, 200), 0, 0, Inter.Linear);

        return (normalizado, cor);
    }

    // Cópia colorida da imagem com um retângulo verde em volta de cada rosto (só para exibir).
    public Mat DesenharRostos(Mat imagem)
    {
        var saida = new Mat();
        if (imagem.NumberOfChannels == 1)
            CvInvoke.CvtColor(imagem, saida, ColorConversion.Gray2Bgr);
        else
            imagem.CopyTo(saida);

        foreach (Rectangle r in DetectarRostos(imagem))
            CvInvoke.Rectangle(saida, r, new MCvScalar(0, 200, 0), 3);

        return saida;
    }

    public void Dispose() => _cascata.Dispose();
}
