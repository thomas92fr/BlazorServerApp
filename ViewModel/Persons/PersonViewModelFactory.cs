using Infrastructure.Factory;
using Infrastructure.Repository;

namespace ViewModel.Persons;

/// <summary>
/// Factory for creating PersonViewModel instances.
/// CONVENTION: Must be named {EntityName}ViewModelFactory in same namespace as ViewModel.
/// </summary>
public class PersonViewModelFactory : IEntityViewModelFactory<Model.Entities.Person, PersonViewModel>
{
    public PersonViewModel Create(Model.Entities.Person entity, IRepository repository)
    {
        return new PersonViewModel(entity, repository);
    }
}
