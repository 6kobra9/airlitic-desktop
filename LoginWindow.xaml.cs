using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace AirLiticApp;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        var login = LoginTextBox.Text.Trim();
        var password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "Введіть логін і пароль.";
            return;
        }

        try
        {
            using var db = new Data.AppDbContext();
            var passwordHash = ComputeSha256(password);

            var user = db.Users.FirstOrDefault(u => u.Login == login && u.PasswordHash == passwordHash);

            if (user == null)
            {
                ErrorText.Text = "Невірний логін або пароль.";
                return;
            }

            var mainWindow = new MainWindow(user);
            mainWindow.Show();
            Close();
        }
        catch (System.Exception ex)
        {
            ErrorText.Text = $"Помилка підключення до БД: {ex.Message}";
        }
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}

