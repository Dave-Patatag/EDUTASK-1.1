using EDUTASK_1._1.Services;
using System.Data;
using System.Windows.Input;
using TeacherTaskItem = EDUTASK_1._1.Models.TeacherTaskItem;

namespace EDUTASK_1._1.Views
{
    public partial class TeacherTasksPage : ContentPage
    {
        private DatabaseService _db = new DatabaseService();
        private List<TeacherTaskItem> _allTasks = new List<TeacherTaskItem>();
        private Dictionary<int, int> _teacherIdMap = new Dictionary<int, int>();

        public TeacherTasksPage()
        {
            InitializeComponent();
            LoadTeachers();
        }

        // FIXED: Added 'async' keyword and changed return type to 'async void'
        private async void LoadTeachers()
        {
            try
            {
                var dt = await _db.GetAllTeachersAsync();
                var teachers = new List<string>();
                _teacherIdMap.Clear();

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    string fullName = dt.Rows[i]["FirstName"] + " " + dt.Rows[i]["LastName"];
                    teachers.Add(fullName);
                    _teacherIdMap[i] = Convert.ToInt32(dt.Rows[i]["TeacherID"]);
                }
                TeacherPicker.ItemsSource = teachers;

                if (teachers.Count > 0)
                    TeacherPicker.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Teachers couldn't load", "We couldn't load the teacher list. Please try again.", "OK");
            }
        }

        private async void OnTeacherSelected(object sender, EventArgs e)
        {
            if (TeacherPicker.SelectedIndex == -1) return;

            int teacherId = _teacherIdMap[TeacherPicker.SelectedIndex];
            await LoadTeacherTasks(teacherId);
        }

        private async Task LoadTeacherTasks(int teacherId)
        {
            try
            {
                var dt = await _db.GetTeacherTasksAsync(teacherId);
                _allTasks.Clear();

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    int assignmentId = Convert.ToInt32(dt.Rows[i]["AssignmentID"]);
                    bool isAcknowledged = Convert.ToBoolean(dt.Rows[i]["IsAcknowledged"]);
                    string completion = dt.Rows[i]["CompletionStatus"].ToString();

                    string status = "Pending";
                    if (completion == "Completed")
                        status = "✓ Completed";
                    else if (isAcknowledged)
                        status = "✓ Acknowledged";

                    var item = new TeacherTaskItem
                    {
                        AssignmentID = assignmentId,
                        Title = dt.Rows[i]["Title"].ToString(),
                        Deadline = Convert.ToDateTime(dt.Rows[i]["Deadline"]).ToString("MMM dd, yyyy"),
                        Priority = dt.Rows[i]["Priority"].ToString(),
                        Status = status,
                        PriorityColor = GetPriorityColor(dt.Rows[i]["Priority"].ToString()),
                        StatusColor = completion == "Completed" ? Colors.Green : (isAcknowledged ? Colors.Blue : Colors.Orange),
                        ShowAcknowledge = !isAcknowledged && completion != "Completed",
                        AcknowledgeCommand = new Command(async () => await AcknowledgeTask(assignmentId))
                    };
                    _allTasks.Add(item);
                }

                TaskListView.ItemsSource = new List<TeacherTaskItem>(_allTasks);
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Tasks couldn't load", "We couldn't load the tasks. Please try again.", "OK");
            }
        }

        private async Task AcknowledgeTask(int assignmentId)
        {
            try
            {
                bool success = await _db.AcknowledgeTaskAsync(assignmentId);
                if (success)
                {
                    await UiAlertService.ShowAsync(this, "Task acknowledged", "The task is ready for you to work on.", "OK");
                    await LoadTeacherTasks(_teacherIdMap[TeacherPicker.SelectedIndex]);
                }
            }
            catch (Exception)
            {
                await UiAlertService.ShowAsync(this, "Task couldn't be acknowledged", "We couldn't update this task. Please try again.", "OK");
            }
        }

        private Color GetPriorityColor(string priority)
        {
            return priority switch
            {
                "High" => Color.FromArgb("#DC2626"),
                "Medium" => Color.FromArgb("#EAB308"),
                "Low" => Color.FromArgb("#22A447"),
                _ => Colors.Gray
            };
        }
    }

}
