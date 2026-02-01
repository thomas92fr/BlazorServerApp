namespace Model.Entities;

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
}
