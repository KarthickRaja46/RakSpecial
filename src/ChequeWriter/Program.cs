using ChequeWriter.Core;

namespace ChequeWriter;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.ThreadException += (_, e) => ShowError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => ShowError(e.ExceptionObject as Exception);

        var path = DataStore.DefaultPath(AppContext.BaseDirectory);
        // First start: bring over payees, register and layout from version 1 ("Cheque Writer"), if it was used here.
        try { DataStore.MigrateFrom(DataStore.OldVersionPath(), path); } catch { /* start fresh */ }

        DataStore store;
        try
        {
            store = new DataStore(path);
        }
        catch (Exception ex)
        {
            var bak = path + ".bak";
            var msg = $"The data file could not be opened:\n{path}\n\n{ex.Message}";
            if (File.Exists(bak) && MessageBox.Show(msg + "\n\nOpen the previous copy (.bak) instead?", AppInfo.Name,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                File.Copy(path, path + ".damaged-" + DateTime.Now.ToString("yyyyMMddHHmmss"), overwrite: true);
                File.Copy(bak, path, overwrite: true);
                store = new DataStore(path);
            }
            else
            {
                MessageBox.Show(msg, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        Application.Run(new MainForm(store));
    }

    static void ShowError(Exception? ex) =>
        MessageBox.Show("Something went wrong:\n\n" + ex?.Message, AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
}
