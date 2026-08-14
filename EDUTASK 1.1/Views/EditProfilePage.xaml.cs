using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.ViewModels;
using System.Net.Mail;

namespace EDUTASK_1._1.Views
{
    public partial class EditProfilePage : ContentPage
    {
        private readonly EditProfileViewModel _viewModel;
        private readonly DatabaseService _database = new();
        private User? _user;
        private Teachers? _teacher;

        public EditProfilePage(User user) : this()
        {
            _user = user;
            PopulateForm(
                user.FirstName,
                user.LastName,
                user.ContactNumber,
                user.Email,
                user.Username,
                user.Birthdate,
                user.ProfilePhotoPath,
                $"user-{user.UserID}");
        }

        public EditProfilePage(Teachers teacher) : this()
        {
            _teacher = teacher;
            PopulateForm(
                teacher.FirstName,
                teacher.LastName,
                teacher.ContactNumber,
                teacher.Email,
                teacher.Username,
                teacher.Birthdate,
                teacher.ProfilePhotoPath,
                $"teacher-{teacher.TeacherID}");
        }

        private EditProfilePage()
        {
            InitializeComponent();
            _viewModel = new EditProfileViewModel();
            BindingContext = _viewModel;
            PhoneEntry.TextChanged += OnPhoneNumberTextChanged;
        }

        private void OnPhoneNumberTextChanged(object? sender, TextChangedEventArgs e)
        {
            string digitsOnly = new string((e.NewTextValue ?? string.Empty)
                .Where(char.IsDigit)
                .Take(11)
                .ToArray());

            if (!string.Equals(e.NewTextValue, digitsOnly, StringComparison.Ordinal))
                PhoneEntry.Text = digitsOnly;
        }

        private void PopulateForm(
            string firstName,
            string lastName,
            string contactNumber,
            string email,
            string username,
            DateTime? birthdate,
            string profilePhotoPath,
            string avatarSeed)
        {
            _viewModel.AvatarSeed = avatarSeed;
            _viewModel.FullName = $"{firstName} {lastName}".Trim();
            _viewModel.ContactNumber = contactNumber;
            _viewModel.Email = email;
            _viewModel.Username = username;
            _viewModel.Birthdate = birthdate ?? DateTime.Today.AddYears(-18);
            _viewModel.ProfilePhotoPath = profilePhotoPath;
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }

        private async void OnChangePhotoTapped(object sender, EventArgs e)
        {
            try
            {
                FileResult? photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
                {
                    Title = "Choose a profile photo"
                });

                if (photo is null)
                    return;

                string extension = Path.GetExtension(photo.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                    extension = ".jpg";

                int accountID = _teacher?.TeacherID ?? _user?.UserID
                    ?? throw new InvalidOperationException("No account was selected.");
                string accountType = _teacher is null ? "user" : "teacher";
                string destination = Path.Combine(
                    FileSystem.AppDataDirectory,
                    $"{accountType}_profile_{accountID}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension.ToLowerInvariant()}");

                await using Stream source = await photo.OpenReadAsync();
                await using FileStream target = File.Create(destination);
                await source.CopyToAsync(target);

                _viewModel.ProfilePhotoPath = destination;
            }
            catch (PermissionException)
            {
                await UiAlertService.ShowAsync(this, "Photo access needed", "Allow access to your photos, then try again.", "OK");
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Photo couldn't be selected", "We couldn't open that photo. Please choose another one.", "OK");
            }
        }

        private async void OnSaveChangesClicked(object sender, EventArgs e)
        {
            try
            {
                ValidateProfile();

                bool updated;
                if (_teacher is not null)
                {
                    updated = await _database.UpdateTeacherProfileAsync(
                        _teacher.TeacherID,
                        _viewModel.FullName,
                        _viewModel.ContactNumber,
                        _viewModel.Email,
                        _viewModel.Username,
                        _viewModel.Birthdate,
                        _viewModel.ProfilePhotoPath);
                }
                else if (_user is not null)
                {
                    updated = await _database.UpdateUserProfileAsync(
                        _user.UserID,
                        _viewModel.FullName,
                        _viewModel.ContactNumber,
                        _viewModel.Email,
                        _viewModel.Username,
                        _viewModel.Birthdate,
                        _viewModel.ProfilePhotoPath);

                    var refreshedUser = await _database.GetUserByIdAsync(_user.UserID);
                    if (refreshedUser is not null)
                        UserSessionService.SetCurrentUser(refreshedUser);
                }
                else
                {
                    throw new InvalidOperationException("No account was selected.");
                }

                if (!updated)
                    throw new InvalidOperationException("The account no longer exists.");

                await UiAlertService.ShowAsync(this, "Profile updated", "Your changes have been saved.", "OK");
                await Navigation.PopAsync();
            }
            catch (FormatException)
            {
                await UiAlertService.ShowAsync(this, "Invalid email", "Please enter a valid email address.", "OK");
            }
            catch (ArgumentException ex)
            {
                await UiAlertService.ShowAsync(this, "Check your details", ex.Message, "OK");
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Changes couldn't be saved", "We couldn't update your profile. Please try again.", "OK");
            }
        }

        private void ValidateProfile()
        {
            if (string.IsNullOrWhiteSpace(_viewModel.FullName))
                throw new ArgumentException("Full name is required.");
            if (string.IsNullOrWhiteSpace(_viewModel.ContactNumber))
                throw new ArgumentException("Phone number is required.");
            string phoneNumber = _viewModel.ContactNumber.Trim();
            if (phoneNumber.Length > 11)
                throw new ArgumentException("Phone number cannot exceed 11 digits.");
            if (phoneNumber.Any(character => !char.IsDigit(character)))
                throw new ArgumentException("Phone number must contain digits only.");
            if (string.IsNullOrWhiteSpace(_viewModel.Email))
                throw new ArgumentException("Email is required.");

            string email = _viewModel.Email.Trim();
            _ = new MailAddress(email);

            if (string.IsNullOrWhiteSpace(_viewModel.Username))
                throw new ArgumentException("Username is required.");
            if (!_viewModel.Username.Trim().StartsWith('@'))
                throw new ArgumentException("Username must start with @.");
            if (_viewModel.Birthdate.Date >= DateTime.Today)
                throw new ArgumentException("Please select a valid birthdate.");
        }
    }
}
