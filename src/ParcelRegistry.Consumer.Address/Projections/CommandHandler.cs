namespace ParcelRegistry.Consumer.Address.Projections
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Autofac;
    using Be.Vlaanderen.Basisregisters.CommandHandling;
    using Be.Vlaanderen.Basisregisters.CommandHandling.Idempotency;
    using Be.Vlaanderen.Basisregisters.GrAr.Provenance;
    using Microsoft.Extensions.Logging;

    public class CommandHandler
    {
        private readonly ILifetimeScope _container;
        private readonly ILogger<CommandHandler> _logger;

        public CommandHandler(ILifetimeScope container, ILoggerFactory loggerFactory)
        {
            _container = container;
            _logger = loggerFactory.CreateLogger<CommandHandler>();
        }

        public virtual async Task Handle<T>(T command, CancellationToken cancellationToken)
            where T : class, IHasCommandProvenance
        {
            _logger.LogDebug($"Handling {command.GetType().FullName}");

            await using var scope = _container.BeginLifetimeScope();

            var resolver = scope.Resolve<ICommandHandlerResolver>();
            _ = await resolver.Dispatch(command.CreateCommandId(), command, cancellationToken: cancellationToken);

            _logger.LogDebug($"Handled {command.GetType().FullName}");
        }

        public virtual async Task HandleIdempotent<T>(T command, CancellationToken cancellationToken)
            where T : class, IHasCommandProvenance
        {
            _logger.LogDebug($"Idempotently handling {command.GetType().FullName}");

            await using var scope = _container.BeginLifetimeScope();

            var resolver = scope.Resolve<IIdempotentCommandHandler>();
            _ = await resolver.Dispatch(
                command.CreateCommandId(),
                command,
                new Dictionary<string, object>(),
                cancellationToken: cancellationToken);

            _logger.LogDebug($"Idempotently handled {command.GetType().FullName}");
        }
    }
}
