using BiometriaFacial.Biometria;
using BiometriaFacial.Data;
using BiometriaFacial.Servicos;
using BiometriaFacial.UI;

namespace BiometriaFacial;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            string baseDir = AppContext.BaseDirectory;
            var banco = new Banco(Path.Combine(baseDir, "dados"));
            using var pre = new Preprocessador(Path.Combine(baseDir, "haarcascade_frontalface_default.xml"));
            using var bio = new BiometriaServico(banco, pre);

            Application.Run(new MainForm(banco, pre, bio));
        }
        catch (Exception ex)
        {
            MessageBox.Show("Não foi possível iniciar o sistema:\n\n" + ex.Message,
                "Erro ao iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
