using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using BlazorServerApp.Model.UnitOfWork;
using BlazorServerApp.ViewModel;

namespace BlazorServerApp.ViewMCP.Tools;

[McpServerToolType]
public class PersonTools
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [McpServerTool]
    [Description("Gets all persons from the database. Returns JSON array with id, name, age, isTeacher, score, satisfaction.")]
    public static string GetAllPersons(IUnitOfWorkFactory unitOfWorkFactory)
    {
        using var unitOfWork = unitOfWorkFactory.Create();
        var viewModel = new PersonListViewModel(unitOfWork);
        viewModel.Initialize();

        var persons = viewModel.Persons.Collection.Select(p => new
        {
            id = p.Id.Value,
            name = p.Name.Value,
            age = p.Age.Value,
            isTeacher = p.IsTeacher.Value,
            score = p.Score.Value,
            satisfaction = p.Satisfaction.Value,
            workDurationHours = p.WorkDuration.Value.TotalHours,
            startDate = p.StartDateTime.Value.ToString("yyyy-MM-dd"),
            endDate = p.EndDateTime.Value.ToString("yyyy-MM-dd"),
            mentorId = p.Model.MentorId
        }).ToList();

        return JsonSerializer.Serialize(
            new { persons, count = persons.Count },
            JsonOptions);
    }
}
