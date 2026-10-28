using Emgu.CV;
using Emgu.CV.Face;
using Emgu.CV.Util;

namespace BiometriaFacial.Biometria;

// Encapsula o LBPH (Local Binary Patterns Histograms) do OpenCV.
// A "distância" devolvida é a diferença entre os histogramas: quanto MENOR, mais parecido.
public class ReconhecedorFacial : IDisposable
{
    private LBPHFaceRecognizer? _lbph;

    public bool Treinado => _lbph != null;

    // amostras: (id da pessoa, rosto já normalizado)
    public void Treinar(IList<(int PessoaId, Mat Rosto)> amostras)
    {
        _lbph?.Dispose();
        _lbph = null;

        if (amostras.Count == 0) return;

        using var imagens = new VectorOfMat();
        using var rotulos = new VectorOfInt();
        foreach (var (pessoaId, rosto) in amostras)
        {
            imagens.Push(rosto);
            rotulos.Push(new[] { pessoaId });
        }

        // raio 1, 8 vizinhos, grade 8x8, sem limiar interno (o limiar é nosso)
        var novo = new LBPHFaceRecognizer(1, 8, 8, 8, double.MaxValue);
        novo.Train(imagens, rotulos);
        _lbph = novo;
    }

    public (int PessoaId, double Distancia) Prever(Mat rosto)
    {
        if (_lbph == null) throw new ModeloNaoTreinadoException();

        var resultado = _lbph.Predict(rosto);
        return (resultado.Label, resultado.Distance);
    }

    public void Dispose()
    {
        _lbph?.Dispose();
        _lbph = null;
    }
}
