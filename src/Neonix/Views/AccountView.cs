using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Neonix.Services;

namespace Neonix.Views
{
    /// <summary>Sign in / create account (local to this PC) and account management.</summary>
    public class AccountView : UserControl
    {
        public AccountView()
        {
            AccountService.Changed += OnChanged;
            Unloaded += (_, _) => AccountService.Changed -= OnChanged;
            Build();
        }

        void OnChanged() => App.RunOnUi(Build);

        void Build()
        {
            Content = AccountService.Current is { } account ? SignedInPage(account) : SignedOutPage();
        }

        // ───────────────────────── signed out ─────────────────────────

        UIElement SignedOutPage()
        {
            bool signUp = false;

            var name = new TextBox();
            var email = new TextBox();
            var password = new PasswordBox();
            var confirm = new PasswordBox();
            var strength = new Ui.Meter(6) { Margin = new Thickness(0, 8, 0, 0) };
            var strengthLabel = Ui.Label("", 11, true);
            var error = new TextBlock
            {
                Foreground = Ui.B("Accent"), FontSize = 12.5, TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed,
            };
            var form = new StackPanel { Width = 340 };
            var submit = new Button { Style = (Style)Application.Current.FindResource("NeonButton"), Margin = new Thickness(0, 16, 0, 0), HorizontalAlignment = HorizontalAlignment.Stretch };

            // The card that shakes on a wrong password.
            Border card = null!;

            // Sign in / Create account switch with a sliding underline.
            var indicator = new Border
            {
                Height = 3, Width = 150, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left,
                Background = Ui.B("NeonGradient"), RenderTransform = new TranslateTransform(0, 0),
            };
            TextBlock Tab(string text, int col)
            {
                var t = new TextBlock
                {
                    Text = text, FontSize = 14, FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand, Background = Brushes.Transparent,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 0, 0, 10),
                };
                Grid.SetColumn(t, col);
                return t;
            }
            var tabSignIn = Tab("Sign in", 0);
            var tabSignUp = Tab("Create account", 1);
            var tabs = new Grid { Width = 300, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16) };
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            tabs.ColumnDefinitions.Add(new ColumnDefinition());
            tabs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            tabs.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(indicator, 1);
            Grid.SetColumnSpan(indicator, 2);
            tabs.Children.Add(tabSignIn);
            tabs.Children.Add(tabSignUp);
            tabs.Children.Add(indicator);

            UIElement Field(string label, UIElement box)
            {
                var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                sp.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = Ui.B("Muted"), Margin = new Thickness(2, 0, 0, 5) });
                sp.Children.Add(box);
                return sp;
            }

            void Layout(bool animate)
            {
                tabSignIn.Foreground = signUp ? Ui.B("Muted") : Ui.B("Text");
                tabSignUp.Foreground = signUp ? Ui.B("Text") : Ui.B("Muted");
                submit.Content = signUp ? "Create account" : "Sign in";
                error.Visibility = Visibility.Collapsed;

                var tt = (TranslateTransform)indicator.RenderTransform;
                double to = signUp ? 150 : 0;
                if (animate)
                    tt.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(to, TimeSpan.FromMilliseconds(300))
                    { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                else tt.X = to;

                form.Children.Clear();
                int i = 0;
                if (signUp) { var f = Field("Your name (optional)", name); form.Children.Add(f); if (animate) Ui.Reveal(f, i++); }
                var fe = Field("Email", email); form.Children.Add(fe); if (animate) Ui.Reveal(fe, i++);
                var fp = Field("Password", password); form.Children.Add(fp); if (animate) Ui.Reveal(fp, i++);
                if (signUp)
                {
                    // Strength meter lives under the password field.
                    var sp = new StackPanel { Margin = new Thickness(0, -6, 0, 12) };
                    sp.Children.Add(strength);
                    sp.Children.Add(strengthLabel);
                    form.Children.Add(sp);
                    var fc = Field("Confirm password", confirm); form.Children.Add(fc); if (animate) Ui.Reveal(fc, i++);
                }
                form.Children.Add(error);
                form.Children.Add(submit);
            }

            async void Submit()
            {
                error.Visibility = Visibility.Collapsed;
                submit.IsEnabled = false;
                (bool Ok, string? Error) result;
                if (signUp)
                    result = password.Password != confirm.Password
                        ? (false, "The passwords don't match.")
                        : AccountService.SignUp(email.Text, name.Text, password.Password);
                else
                    result = await AccountService.SignInAsync(email.Text, password.Password);
                submit.IsEnabled = true;

                if (!result.Ok)
                {
                    error.Text = result.Error;
                    error.Visibility = Visibility.Visible;
                    Ui.PopIn(error);
                    Ui.Shake(card);
                }
                // On success the account service raises Changed and this page is rebuilt.
            }

            submit.Click += (_, _) => Submit();
            KeyEventHandler enter = (_, e) => { if (e.Key == Key.Enter) Submit(); };
            email.KeyDown += enter;
            password.KeyDown += enter;
            confirm.KeyDown += enter;

            password.PasswordChanged += (_, _) =>
            {
                int s = AccountService.Strength(password.Password);
                strength.Set(s);
                strengthLabel.Text = password.Password.Length == 0 ? "" : s < 40 ? "Weak" : s < 70 ? "Okay" : "Strong";
            };

            tabSignIn.MouseLeftButtonUp += (_, _) => { if (signUp) { signUp = false; Layout(true); } };
            tabSignUp.MouseLeftButtonUp += (_, _) => { if (!signUp) { signUp = true; Layout(true); } };

            var inner = new StackPanel();
            inner.Children.Add(tabs);
            inner.Children.Add(form);
            card = Ui.Card(inner);
            card.HorizontalAlignment = HorizontalAlignment.Left;
            card.Padding = new Thickness(28, 24, 28, 28);
            card.RenderTransform = new TranslateTransform();
            Layout(false);

            return Ui.Page(
                Ui.Title("Account"),
                Ui.Sub("Create an account to keep your own profiles separate from other people who use this PC."),
                card,
                LocalNote());
        }

        // ───────────────────────── signed in ─────────────────────────

        UIElement SignedInPage(Account account)
        {
            string initial = account.DisplayName.Length > 0 ? account.DisplayName[..1].ToUpperInvariant() : "?";

            var avatar = new Grid { Width = 72, Height = 72, Margin = new Thickness(0, 0, 20, 0) };
            avatar.Children.Add(new System.Windows.Shapes.Ellipse { Fill = Ui.AnimatedGradient(6) });
            avatar.Children.Add(new TextBlock
            {
                Text = initial, FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            });

            var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            info.Children.Add(new TextBlock { Text = account.DisplayName, FontSize = 20, FontWeight = FontWeights.SemiBold, Foreground = Ui.B("Text") });
            info.Children.Add(Ui.Label(account.Email, 13, true));
            info.Children.Add(Ui.Label($"Signed in · member since {account.Created.ToLocalTime():d MMM yyyy}", 12, true));

            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(avatar);
            header.Children.Add(info);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
            buttons.Children.Add(Ui.Btn("Sign out", (_, _) => AccountService.SignOut()));
            buttons.Children.Add(Ui.Btn("Delete account", (_, _) =>
            {
                var answer = MessageBox.Show(
                    "Delete this account and every profile it owns? This can't be undone.",
                    "Delete account", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (answer == MessageBoxResult.Yes) AccountService.DeleteCurrent();
            }));

            var top = new StackPanel();
            top.Children.Add(header);
            top.Children.Add(buttons);

            // Change password
            var current = new PasswordBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
            var next = new PasswordBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
            var again = new PasswordBox { Width = 280, HorizontalAlignment = HorizontalAlignment.Left };
            var message = new TextBlock { FontSize = 12.5, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
            var change = Ui.Btn("Change password", null!);
            change.Margin = new Thickness(0, 14, 0, 0);
            change.HorizontalAlignment = HorizontalAlignment.Left;
            change.Click += async (_, _) =>
            {
                message.Visibility = Visibility.Collapsed;
                (bool Ok, string? Error) r = next.Password != again.Password
                    ? (false, "The new passwords don't match.")
                    : await AccountService.ChangePasswordAsync(current.Password, next.Password);
                message.Text = r.Ok ? "Password changed." : r.Error;
                message.Foreground = r.Ok ? Ui.B("Accent2") : Ui.B("Accent");
                message.Visibility = Visibility.Visible;
                Ui.PopIn(message);
                if (r.Ok) { current.Clear(); next.Clear(); again.Clear(); }
            };

            var pw = new StackPanel();
            pw.Children.Add(Ui.Row("Current password", current, 160));
            pw.Children.Add(Ui.Row("New password", next, 160));
            pw.Children.Add(Ui.Row("Repeat new password", again, 160));
            pw.Children.Add(change);
            pw.Children.Add(message);

            return Ui.Page(
                Ui.Title("Account"),
                Ui.Sub("You're signed in. New profiles you create are private to this account."),
                Ui.Card(top),
                Ui.Card(pw, "Change password"),
                LocalNote());
        }

        static UIElement LocalNote() => Ui.Card(Ui.Label(
            "Accounts are stored on this PC only (in your AppData folder). They separate each person's profiles; " +
            "they don't sync to other computers yet. Your password is never saved, only a salted hash of it, " +
            "so there's no way to recover a forgotten password.", 12, true), "How accounts work");
    }
}
