namespace BlazorServerApp.Model.Entities;

/// <summary>
/// Sample entity demonstrating the MVVM pattern.
/// </summary>
public class Person : IEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsTeacher { get; set; }
    public DateTime StartDateTime { get; set; } = DateTime.Now;
    public DateTime EndDateTime { get; set; } = DateTime.Now.AddYears(1);

    public decimal Score { get; set; }
    public TimeSpan WorkDuration { get; set; }
    public int Satisfaction { get; set; } = 50;

    public string Comment { get; set; } = string.Empty;

    public string? Cv { get; set; }

    public int? MentorId { get; set; }

    private Person? _mentor;
    public virtual Person? Mentor
    {
        get => _mentor;
        set
        {
            _mentor = value;
            var newId = value?.Id;
            if (MentorId != newId)
                MentorId = newId;
        }
    }
}
