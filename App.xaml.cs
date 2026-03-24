using System.Windows;

namespace kr2pks
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            DispatcherUnhandledException += (sender, args) =>
            {
                MessageBox.Show($"Необработанная ошибка: {args.Exception.Message}\n\n" +
                              $"Стек вызовов: {args.Exception.StackTrace}",
                              "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };
        }
    }
}