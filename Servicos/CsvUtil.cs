using System.Text;

namespace BiometriaFacial.Servicos;

// Exporta tabelas para CSV com ';' (o Excel em português abre direto).
public static class CsvUtil
{
    public static void Salvar(string caminho, IEnumerable<string> cabecalho, IEnumerable<IEnumerable<string>> linhas)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(";", cabecalho.Select(Escapar)));
        foreach (var linha in linhas)
            sb.AppendLine(string.Join(";", linha.Select(Escapar)));

        File.WriteAllText(caminho, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Escapar(string valor)
    {
        if (valor.Contains(';') || valor.Contains('"') || valor.Contains('\n'))
            return "\"" + valor.Replace("\"", "\"\"") + "\"";
        return valor;
    }
}
