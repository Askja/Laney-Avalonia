using Avalonia.Controls;
using Avalonia.Interactivity;
using ELOR.Laney.Core;
using ELOR.Laney.Extensions;
using ELOR.Laney.Helpers;
using ELOR.Laney.Views.Modals;
using ELOR.VKAPILib;
using ELOR.VKAPILib.Objects.Auth;
using System;
using System.Threading.Tasks;
using VKUI.Controls;

namespace ELOR.Laney.Views.SignIn {
    public partial class DirectAuthPage : VKUI.Controls.Page {
        private bool isWorking;

        public DirectAuthPage() {
            InitializeComponent();
        }

        private async void BackButton_Click(object sender, RoutedEventArgs e) {
            await NavigationRouter.BackAsync();
        }

        private async void SignInButton_Click(object sender, RoutedEventArgs e) {
            await SignInAsync();
        }

        private async Task SignInAsync() {
            if (isWorking) return;

            string login = LoginBox.Text?.Trim();
            string password = PasswordBox.Text;

            if (String.IsNullOrWhiteSpace(login) || String.IsNullOrWhiteSpace(password)) {
                ShowInlineError($"{Assets.i18n.Resources.da_login} / {Assets.i18n.Resources.da_password}");
                return;
            }

            SetWorking(true);
            try {
                DirectAuthResponse response = await RequestTokenAsync(login, password);
                await ContinueWithResponseAsync(response, login, password);
            } catch (Exception ex) {
                await ExceptionHelper.ShowErrorDialogAsync(TopLevel.GetTopLevel(this) as Window, ex, true);
            } finally {
                SetWorking(false);
            }
        }

        private async Task ContinueWithResponseAsync(DirectAuthResponse response, string login, string password) {
            string code = null;
            string captchaSid = null;
            string captchaKey = null;

            for (int attempt = 0; attempt < 4; attempt++) {
                if (response != null && !String.IsNullOrWhiteSpace(response.AccessToken)) {
                    await NavigationRouter.NavigateToAsync(new PostDirectAuthPage(response.UserId, response.AccessToken));
                    return;
                }

                if (response == null) throw new InvalidOperationException("VK вернул пустой ответ авторизации.");

                if (IsCaptchaRequired(response)) {
                    captchaSid = response.CaptchaSid;
                    captchaKey = await PromptAsync("Captcha", response.CaptchaImg ?? response.ErrorDescription, false);
                    if (String.IsNullOrWhiteSpace(captchaKey)) return;
                } else if (IsValidationRequired(response)) {
                    code = await PromptAsync(Assets.i18n.Resources.da_2fa_confirm, GetValidationText(response), false);
                    if (String.IsNullOrWhiteSpace(code)) return;
                } else {
                    throw new InvalidOperationException(GetAuthErrorText(response));
                }

                response = await RequestTokenAsync(login, password, code, captchaSid, captchaKey);
            }

            throw new InvalidOperationException(GetAuthErrorText(response));
        }

        private static Task<DirectAuthResponse> RequestTokenAsync(string login, string password, string code = null, string captchaSid = null, string captchaKey = null) {
            return DirectAuth.GetAccessTokenAsync(
                AuthManager.CLIENT_ID,
                AuthManager.CLIENT_SECRET,
                AuthManager.SCOPE,
                login,
                password,
                App.UserAgent,
                Assets.i18n.Resources.lang,
                code,
                captchaSid,
                captchaKey,
                LNetExtensions.SendRequestToAPIViaLNetAsync);
        }

        private async Task<string> PromptAsync(string header, string text, bool passwordMode) {
            TextBox input = new TextBox {
                PasswordChar = passwordMode ? '*' : default,
                PlaceholderText = header
            };

            VKUIDialog dialog = new VKUIDialog(header, text, [Assets.i18n.Resources.close, Assets.i18n.Resources.da_2fa_confirm], 2) {
                DialogContent = input
            };
            int result = await dialog.ShowDialog<int>(TopLevel.GetTopLevel(this) as Window);
            return result == 2 ? input.Text?.Trim() : null;
        }

        private static bool IsValidationRequired(DirectAuthResponse response) {
            return String.Equals(response.Error, "need_validation", StringComparison.OrdinalIgnoreCase)
                || !String.IsNullOrWhiteSpace(response.ValidationType);
        }

        private static bool IsCaptchaRequired(DirectAuthResponse response) {
            return String.Equals(response.Error, "need_captcha", StringComparison.OrdinalIgnoreCase)
                || !String.IsNullOrWhiteSpace(response.CaptchaSid);
        }

        private static string GetValidationText(DirectAuthResponse response) {
            if (String.Equals(response.ValidationType, "2fa_sms", StringComparison.OrdinalIgnoreCase) && !String.IsNullOrWhiteSpace(response.PhoneMask)) {
                return String.Format(Assets.i18n.Resources.da_2fa_sms, response.PhoneMask);
            }

            return Assets.i18n.Resources.da_2fa_app;
        }

        private static string GetAuthErrorText(DirectAuthResponse response) {
            if (!String.IsNullOrWhiteSpace(response?.ErrorDescription)) return response.ErrorDescription;
            if (!String.IsNullOrWhiteSpace(response?.Error)) return response.Error;
            return Assets.i18n.Resources.error;
        }

        private void SetWorking(bool value) {
            isWorking = value;
            Loading.IsVisible = value;
            SignInButton.IsEnabled = !value;
            LoginBox.IsEnabled = !value;
            PasswordBox.IsEnabled = !value;
            BackButton.IsEnabled = !value;
            if (value) HideInlineError();
        }

        private void ShowInlineError(string text) {
            ErrorText.Text = text;
            ErrorText.IsVisible = true;
        }

        private void HideInlineError() {
            ErrorText.Text = String.Empty;
            ErrorText.IsVisible = false;
        }
    }
}
