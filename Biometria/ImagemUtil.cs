using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Util;

namespace BiometriaFacial.Biometria;

// Funções de apoio para ler, gravar e exibir imagens.
// Lemos e gravamos pelos bytes (File.ReadAllBytes / WriteAllBytes) porque o
// Imread/Imwrite do OpenCV costuma falhar em caminhos com acento no Windows.
public static class ImagemUtil
{
    public static Mat Carregar(string caminho, ImreadModes modo = ImreadModes.Color)
    {
        if (!File.Exists(caminho))
            throw new ImagemInvalidaException("Arquivo não encontrado: " + caminho);

        byte[] bytes = File.ReadAllBytes(caminho);
        var imagem = new Mat();
        CvInvoke.Imdecode(bytes, modo, imagem);

        if (imagem.IsEmpty)
        {
            imagem.Dispose();
            throw new ImagemInvalidaException("O arquivo selecionado não é uma imagem válida: " + Path.GetFileName(caminho));
        }
        return imagem;
    }

    public static void SalvarPng(Mat imagem, string caminho)
    {
        using var buffer = new VectorOfByte();
        CvInvoke.Imencode(".png", imagem, buffer);
        File.WriteAllBytes(caminho, buffer.ToArray());
    }

    // Converte Mat (OpenCV) em Bitmap (Windows Forms) para mostrar na tela.
    public static Bitmap ParaBitmap(Mat imagem)
    {
        using var buffer = new VectorOfByte();
        CvInvoke.Imencode(".bmp", imagem, buffer);
        using var ms = new MemoryStream(buffer.ToArray());
        using var temporario = new Bitmap(ms);
        return new Bitmap(temporario);
    }
}
