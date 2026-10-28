namespace BiometriaFacial.Biometria;

// Erros "esperados" do sistema: a interface mostra a mensagem para o usuário
// em vez de deixar o programa fechar.
public class BiometriaException : Exception
{
    public BiometriaException(string mensagem) : base(mensagem) { }
}

public class ImagemInvalidaException : BiometriaException
{
    public ImagemInvalidaException(string mensagem) : base(mensagem) { }
}

public class RostoNaoEncontradoException : BiometriaException
{
    public RostoNaoEncontradoException()
        : base("Nenhum rosto foi detectado na imagem. Tente uma foto mais nítida, de frente e com boa iluminação.") { }
}

public class VariosRostosException : BiometriaException
{
    public VariosRostosException(int quantidade)
        : base($"Foram detectados {quantidade} rostos na imagem. Use uma foto com apenas uma pessoa.") { }
}

public class ModeloNaoTreinadoException : BiometriaException
{
    public ModeloNaoTreinadoException()
        : base("Ainda não há pessoas cadastradas. Faça o cadastro de pelo menos uma pessoa antes.") { }
}
