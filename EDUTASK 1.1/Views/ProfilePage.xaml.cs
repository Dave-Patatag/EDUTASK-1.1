using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.ViewModels;

namespace EDUTASK_1._1.Views
{
    public partial class ProfilePage : ContentPage
    {
        private readonly ProfileViewModel _viewModel;
        private readonly DatabaseService _database = new();
        private User? _user;
        private Teachers? _teacher;

        public ProfilePage(User user) : this()
        {
            _user = user;
            ApplyUser(user);
        }

        public ProfilePage(Teachers teacher) : this()
        {
            _teacher = teacher;
            ApplyTeacher(teacher);
        }

        private ProfilePage()
        {
            InitializeComponent();
            WireTaskIcon();
            _viewModel = new ProfileViewModel();
            BindingContext = _viewModel;
        }

        private void WireTaskIcon()
        {
            if (Content is not Grid pageLayout)
                return;

            var bottomNavigation = pageLayout.Children
                .OfType<Grid>()
                .FirstOrDefault(grid => Grid.GetRow(grid) == 1);

            var taskIcon = bottomNavigation?.Children
                .OfType<Image>()
                .FirstOrDefault(image => image.Source is FileImageSource source &&
                                         string.Equals(source.File, "task.png", StringComparison.OrdinalIgnoreCase));

            if (taskIcon is null)
                return;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnTaskIconTapped;
            taskIcon.GestureRecognizers.Add(tapGesture);
        }

        private void OnTaskIconTapped(object? sender, TappedEventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowDetail(new DirectorStaffDashboardPage());
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();


            try
            {
                if (_teacher is not null)
                {
                    var refreshedTeacher = await _database.GetTeacherByIdAsync(_teacher.TeacherID);
                    if (refreshedTeacher is not null)
                    {
                        _teacher = refreshedTeacher;
                        ApplyTeacher(refreshedTeacher);
                    }
                }
                else
                {
                    var refreshedUser = await UserSessionService.GetCurrentUserAsync(forceRefresh: true);
                    if (refreshedUser is not null)
                    {
                        _user = refreshedUser;
                        ApplyUser(refreshedUser);
                    }
                }
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Profile couldn't refresh", "We couldn't refresh your profile. Please try again.", "OK");
            }
        }

        private void ApplyUser(User user)
        {
            _viewModel.AvatarSeed = $"user-{user.UserID}";
            _viewModel.FullName = $"{user.FirstName} {user.LastName}".Trim();
            _viewModel.Email = user.Email;
            _viewModel.ContactNumber = user.ContactNumber;
            _viewModel.Username = user.Username;
            _viewModel.ProfilePhotoPath = user.ProfilePhotoPath;
            _viewModel.Role = string.IsNullOrWhiteSpace(user.RoleName) ? "Staff" : user.RoleName;
        }

        private void ApplyTeacher(Teachers teacher)
        {
            _viewModel.AvatarSeed = $"teacher-{teacher.TeacherID}";
            _viewModel.FullName = $"{teacher.FirstName} {teacher.LastName}".Trim();
            _viewModel.Email = teacher.Email;
            _viewModel.ContactNumber = teacher.ContactNumber;
            _viewModel.Username = teacher.Username;
            _viewModel.ProfilePhotoPath = teacher.ProfilePhotoPath;
            _viewModel.Role = "Teacher";
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            bool confirm = await UiAlertService.ConfirmAsync(this, "Logout", "Log out of your account?", "Logout", "Cancel");
            if (!confirm)
                return;

            UserSessionService.Clear();
            if (Window is not null)
                Window.Page = new NavigationPage(new LandingPage());
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
private async void OnEditProfileClicked(object sender, EventArgs e)
        {
            if (_teacher is not null)
                await Navigation.PushAsync(new EditProfilePage(_teacher));
            else if (_user is not null)
                await Navigation.PushAsync(new EditProfilePage(_user));
        }

    }
}
