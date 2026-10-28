# Identificação e Autenticação Biométrica Facial (APS – UNIP, Ciência da Computação)

Programa em **C# (.NET 8, Windows Forms)** que cadastra pessoas, identifica (1:N) e autentica (1:1) por reconhecimento facial.
Detecção do rosto: Haar Cascade. Reconhecimento: LBPH (OpenCV via Emgu CV). Banco de dados: SQLite.

## Como rodar

1. Instale o **Visual Studio 2022** com a carga de trabalho **"Desenvolvimento para desktop com .NET"** (ou o .NET 8 SDK).
2. Abra a pasta e dê dois cliques em `BiometriaFacial.csproj` (ou `dotnet run` dentro da pasta).
3. Na primeira vez o Visual Studio baixa sozinho os pacotes NuGet (`Emgu.CV`, `Emgu.CV.runtime.windows`, `Microsoft.Data.Sqlite`). É preciso internet.
4. O projeto já está configurado para x64. Aperte **F5**.
5. Se der erro de DLL do OpenCV ao abrir, instale o *Microsoft Visual C++ Redistributable 2015-2022 (x64)*.

Se o NuGet reclamar da versão de algum pacote, atualize para a mais nova disponível (clique direito no projeto > Gerenciar Pacotes NuGet). O código usa só a API básica, que não mudou entre as versões 4.x.

## Como usar (roteiro para os prints)

1. **Cadastro**: digite o nome, adicione 5 ou mais fotos (arquivo ou webcam) e clique em *Salvar cadastro*.
2. **Identificação (1:N)**: abra uma foto (ou use a webcam). O sistema diz quem é, ou "NÃO RECONHECIDO".
3. **Autenticação (1:1)**: escolha quem a pessoa diz ser e envie a foto. Libera ou nega.
4. **Registro de acessos**: lista tudo que foi feito e exporta para CSV.
5. **Avaliação (FAR/FRR)**: aponte para uma pasta com uma subpasta por pessoa (por exemplo a base AT&T/ORL).
   Gera a tabela de taxas por limiar e sugere o melhor limiar.

O **limiar de distância** (canto superior direito) controla o quanto o sistema é rígido: menor = mais rígido
(menos falsos aceitos, mais falsos rejeitados). O valor 80 é só ponto de partida; use a aba 5 para escolher o seu.

## Estrutura

```
Program.cs                      ponto de entrada
Biometria/Preprocessador.cs     cinza, detecção do rosto, recorte 100x100, equalização
Biometria/ReconhecedorFacial.cs LBPH (treinar e prever)
Biometria/ImagemUtil.cs         ler/gravar imagens, converter para Bitmap
Biometria/Erros.cs              exceções do sistema
Data/Banco.cs                   SQLite (pessoas, amostras, acessos)
Servicos/BiometriaServico.cs    cadastro, identificação 1:N, autenticação 1:1
Servicos/Avaliador.cs           cálculo de FAR, FRR, EER
Servicos/CsvUtil.cs             exportação CSV
UI/MainForm.cs                  tela principal (5 abas)
UI/CapturaForm.cs               janela da webcam
haarcascade_frontalface_default.xml   detector facial (OpenCV, licença Apache 2.0 / BSD)
```

Os dados (banco e fotos recortadas) ficam na pasta `dados` ao lado do executável (`bin\x64\Debug\net8.0-windows\dados`).

## Observações para a apresentação

- Use fotos **autorizadas** das pessoas (os próprios integrantes do grupo). Dado biométrico é dado pessoal sensível pela LGPD.
- A base AT&T/ORL (400 imagens, 40 pessoas, 10 fotos cada) é a mais usada para testar; as fotos já são só o rosto.
