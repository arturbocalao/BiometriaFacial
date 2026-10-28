using Microsoft.Data.Sqlite;

namespace BiometriaFacial.Data;

public record Pessoa(int Id, string Nome, int QtdAmostras, string CriadoEm);

public record Acesso(int Id, string DataHora, string Tipo, string Nome, double Distancia, string Resultado, string Origem);

// Acesso ao SQLite. Cada operação abre e fecha sua própria conexão (simples e seguro).
public class Banco
{
    private readonly string _conexao;

    public string PastaFaces { get; }

    public Banco(string pastaDados)
    {
        Directory.CreateDirectory(pastaDados);
        PastaFaces = Path.Combine(pastaDados, "faces");
        Directory.CreateDirectory(PastaFaces);

        _conexao = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(pastaDados, "biometria.db")
        }.ToString();

        Executar(@"CREATE TABLE IF NOT EXISTS pessoas (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       nome TEXT NOT NULL UNIQUE,
                       criado_em TEXT NOT NULL)");
        Executar(@"CREATE TABLE IF NOT EXISTS amostras (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       pessoa_id INTEGER NOT NULL,
                       caminho TEXT NOT NULL)");
        Executar(@"CREATE TABLE IF NOT EXISTS acessos (
                       id INTEGER PRIMARY KEY AUTOINCREMENT,
                       data_hora TEXT NOT NULL,
                       tipo TEXT NOT NULL,
                       pessoa TEXT NOT NULL,
                       distancia REAL NOT NULL,
                       resultado TEXT NOT NULL,
                       origem TEXT NOT NULL)");
    }

    // ---------- pessoas e amostras ----------

    public int InserirPessoa(string nome)
    {
        // last_insert_rowid() vale só para a conexão atual, por isso o INSERT e o SELECT vão juntos.
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "INSERT INTO pessoas (nome, criado_em) VALUES ($n, $d); SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$n", nome);
        cmd.Parameters.AddWithValue("$d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public bool NomeExiste(string nome) =>
        Convert.ToInt32(Escalar("SELECT COUNT(*) FROM pessoas WHERE nome = $n COLLATE NOCASE", ("$n", nome))) > 0;

    public void InserirAmostra(int pessoaId, string caminho) =>
        Executar("INSERT INTO amostras (pessoa_id, caminho) VALUES ($p, $c)", ("$p", pessoaId), ("$c", caminho));

    public List<Pessoa> ListarPessoas()
    {
        var lista = new List<Pessoa>();
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT p.id, p.nome, COUNT(a.id), p.criado_em
                            FROM pessoas p LEFT JOIN amostras a ON a.pessoa_id = p.id
                            GROUP BY p.id ORDER BY p.nome";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(new Pessoa(r.GetInt32(0), r.GetString(1), r.GetInt32(2), r.GetString(3)));
        return lista;
    }

    public string? ObterNome(int pessoaId)
    {
        object? nome = Escalar("SELECT nome FROM pessoas WHERE id = $i", ("$i", pessoaId));
        return nome as string;
    }

    public List<(int PessoaId, string Caminho)> ListarAmostras()
    {
        var lista = new List<(int, string)>();
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = "SELECT pessoa_id, caminho FROM amostras";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add((r.GetInt32(0), r.GetString(1)));
        return lista;
    }

    // Caminho da cópia colorida (só para exibição) de uma amostra: 1_1.png -> 1_1_cor.png
    public static string CaminhoCor(string caminhoCinza) =>
        Path.Combine(Path.GetDirectoryName(caminhoCinza) ?? "",
                     Path.GetFileNameWithoutExtension(caminhoCinza) + "_cor.png");

    public void ExcluirPessoa(int pessoaId)
    {
        foreach (var (id, caminho) in ListarAmostras())
        {
            if (id != pessoaId) continue;
            foreach (string arquivo in new[] { caminho, CaminhoCor(caminho) })
            {
                try { if (File.Exists(arquivo)) File.Delete(arquivo); } catch (IOException) { /* arquivo em uso: ignora */ }
            }
        }
        Executar("DELETE FROM amostras WHERE pessoa_id = $i", ("$i", pessoaId));
        Executar("DELETE FROM pessoas WHERE id = $i", ("$i", pessoaId));
    }

    // ---------- log de acessos ----------

    public void RegistrarAcesso(string tipo, string pessoa, double distancia, string resultado, string origem) =>
        Executar(@"INSERT INTO acessos (data_hora, tipo, pessoa, distancia, resultado, origem)
                   VALUES ($d, $t, $p, $di, $r, $o)",
            ("$d", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")), ("$t", tipo), ("$p", pessoa),
            ("$di", distancia), ("$r", resultado), ("$o", origem));

    public List<Acesso> ListarAcessos(int limite = 500)
    {
        var lista = new List<Acesso>();
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = @"SELECT id, data_hora, tipo, pessoa, distancia, resultado, origem
                            FROM acessos ORDER BY id DESC LIMIT $l";
        cmd.Parameters.AddWithValue("$l", limite);
        using var r = cmd.ExecuteReader();
        while (r.Read())
            lista.Add(new Acesso(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3),
                                 Math.Round(r.GetDouble(4), 2), r.GetString(5), r.GetString(6)));
        return lista;
    }

    // ---------- auxiliares ----------

    private SqliteConnection Abrir()
    {
        var con = new SqliteConnection(_conexao);
        con.Open();
        return con;
    }

    private void Executar(string sql, params (string Nome, object Valor)[] parametros)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            cmd.Parameters.AddWithValue(nome, valor);
        cmd.ExecuteNonQuery();
    }

    private object? Escalar(string sql, params (string Nome, object Valor)[] parametros)
    {
        using var con = Abrir();
        using var cmd = con.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valor) in parametros)
            cmd.Parameters.AddWithValue(nome, valor);
        return cmd.ExecuteScalar();
    }
}
