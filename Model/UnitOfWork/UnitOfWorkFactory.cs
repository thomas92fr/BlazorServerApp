using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model.Data;

namespace Model.UnitOfWork;

/// <summary>
/// Factory for creating UnitOfWork instances with isolated DbContext.
/// Each call to Create() returns a new UnitOfWork with its own DbContext,
/// suitable for tab isolation in the UI.
/// </summary>
public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbContextFactory<ApplicationDbContext> _contextFactory;
    private readonly ILoggerFactory? _loggerFactory;

    public UnitOfWorkFactory(
        IDbContextFactory<ApplicationDbContext> contextFactory,
        ILoggerFactory? loggerFactory = null)
    {
        _contextFactory = contextFactory;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Creates a new UnitOfWork instance with its own DbContext.
    /// The caller is responsible for disposing the returned UnitOfWork.
    /// </summary>
    public IUnitOfWork Create()
    {
        var context = _contextFactory.CreateDbContext();
        var logger = _loggerFactory?.CreateLogger<UnitOfWork>();
        return new UnitOfWork(context, logger);
    }
}
