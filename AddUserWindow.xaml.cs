using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;

namespace AirLiticApp;

public partial class AddUserWindow : Window
{
    public AddUserWindow()
    {
        InitializeComponent();
        RoleComboBox.ItemsSource = new[] { "Viewer", "Editor", "Admin" };
        RoleComboBox.SelectedIndex = 0;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var login = LoginTextBox.Text.Trim();
        var password = PasswordBox.Password;
        var role = RoleComboBox.SelectedItem as string ?? "Viewer";

        if (string.IsNullOrWhiteSpace(login))
        {
            ErrorText.Text = "Вкажіть логін.";
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "Вкажіть пароль.";
            return;
        }

        using var db = new Data.AppDbContext();

        if (db.Users.Any(u => u.Login == login))
        {
            ErrorText.Text = "Користувач з таким логіном вже існує.";
            return;
        }

        var user = new Models.User
        {
            Login = login,
            PasswordHash = ComputeSha256(password),
            Role = role
        };

        db.Users.Add(user);
        db.SaveChanges();

        DialogResult = true;
        Close();
    }

    private static string ComputeSha256(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return System.BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }
}

