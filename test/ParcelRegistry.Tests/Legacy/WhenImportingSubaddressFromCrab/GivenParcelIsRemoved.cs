namespace ParcelRegistry.Tests.Legacy.WhenImportingSubaddressFromCrab
{
    using System.Collections.Generic;
    using AutoFixture;
    using Be.Vlaanderen.Basisregisters.AggregateSource.Testing;
    using Be.Vlaanderen.Basisregisters.Crab;
    using Fixtures;
    using global::AutoFixture;
    using NodaTime;
    using ParcelRegistry.Legacy;
    using ParcelRegistry.Legacy.Commands.Crab;
    using ParcelRegistry.Legacy.Events;
    using ParcelRegistry.Legacy.Exceptions;
    using Legacy;
    using SnapshotTests;
    using WhenImportingTerrainObjectHouseNumberFromCrab;
    using Xunit;
    using Xunit.Abstractions;
    using WithFixedParcelId = AutoFixture.WithFixedParcelId;

    [Collection("BasedOnSnapshotCollection")]
    public class GivenParcelIsRemoved : ParcelRegistryTest
    {
        private readonly ParcelId _parcelId;
        private readonly string _snapshotId;

        public GivenParcelIsRemoved(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
            Fixture.Customize(new InfrastructureCustomization());
            Fixture.Customize(new WithFixedParcelId());
            Fixture.Customize(new WithNoDeleteModification());
            _parcelId = Fixture.Create<ParcelId>();
            _snapshotId = GetSnapshotIdentifier(_parcelId);
        }

        [Fact]
        public void AddTerrainObjectHouseNumber()
        {
            var command = Fixture.Create<ImportSubaddressFromCrab>()
                .WithLifetime(new CrabLifetime(Fixture.Create<LocalDateTime>(), null))
                .WithModification(CrabModification.Insert);

            Assert(new Scenario()
                .Given(_parcelId,
                    Fixture.Create<ParcelWasRegistered>(),
                    Fixture.Create<ParcelAddressWasAttached>()
                        .WithAddressId(AddressId.CreateFor(command.HouseNumberId)),
                    Fixture.Create<ImportTerrainObjectHouseNumberFromCrab>()
                        .WithHouseNumberId(command.HouseNumberId)
                        .ToLegacyEvent(),
                    Fixture.Create<ParcelWasRemoved>()
                )
                .When(command)
                .Throws(new ParcelRemovedException($"Cannot change removed parcel for parcel id {_parcelId}")));
        }

        [Fact]
        public void AddTerrainObjectHouseNumber_BasedOnSnapshot()
        {
            var command = Fixture.Create<ImportSubaddressFromCrab>()
                .WithLifetime(new CrabLifetime(Fixture.Create<LocalDateTime>(), null))
                .WithModification(CrabModification.Insert);

            var terrainObjectHouseNumberWasImportedFromCrab = Fixture.Create<ImportTerrainObjectHouseNumberFromCrab>()
                .WithHouseNumberId(command.HouseNumberId)
                .ToLegacyEvent();

            Assert(new Scenario()
                .Given(_parcelId, Fixture.Create<ParcelWasRegistered>())
                .Given(_snapshotId,
                    SnapshotBuilder.CreateDefaultSnapshot(_parcelId)
                        .WithAddressIds(new[] { AddressId.CreateFor(command.HouseNumberId) })
                        .WithActiveHouseNumberIdsByTerrainObjectHouseNr(new Dictionary<CrabTerrainObjectHouseNumberId, CrabHouseNumberId>()
                        {
                            { new CrabTerrainObjectHouseNumberId(terrainObjectHouseNumberWasImportedFromCrab.TerrainObjectHouseNumberId), new CrabHouseNumberId(terrainObjectHouseNumberWasImportedFromCrab.HouseNumberId) }
                        })
                        .WithIsRemoved(true)
                        .Build(3, EventSerializerSettings)
                )
                .When(command)
                .Throws(new ParcelRemovedException($"Cannot change removed parcel for parcel id {_parcelId}")));
        }
    }
}
