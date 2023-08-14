using Xunit.Abstractions;
using ProjectOrigin.TestUtils;
using ProjectOrigin.Registry.Server;
using Xunit;
using System.Threading.Tasks;
using System;
using ProjectOrigin.PedersenCommitment;
using FluentAssertions;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.HierarchicalDeterministicKeys;
using System.Collections.Generic;

namespace ProjectOrigin.Electricity.IntegrationTests;

public class FlowTests : GrpcTestBase<Startup>, IClassFixture<ElectricityServiceFixture>
{
    protected ElectricityServiceFixture _verifierFixture;
    protected const string RegistryName = "SomeRegistry";

    protected Registry.V1.RegistryService.RegistryServiceClient Client => new(_grpcFixture.Channel);

    public FlowTests(ElectricityServiceFixture verifierFixture, GrpcTestFixture<Startup> grpcFixture, ITestOutputHelper outputHelper) : base(grpcFixture, outputHelper)
    {
        _verifierFixture = verifierFixture;
        grpcFixture.ConfigureHostConfiguration(new Dictionary<string, string?>()
        {
            {$"Verifiers:project_origin.electricity.v1", _verifierFixture.Url},
            {"RegistryName", RegistryName}
        });
    }

    [Fact]
    public async Task issue_comsumption_certificate_success()
    {
        var owner = Algorithms.Secp256k1.GenerateNewPrivateKey();

        var commitmentInfo = new SecretCommitmentInfo(250);
        var certId = Guid.NewGuid();

        IssuedEvent @event = Helper.CreateIssuedEvent(RegistryName, _verifierFixture.IssuerArea, owner.PublicKey, commitmentInfo, certId);

        var transaction = Helper.SignTransaction(@event.CertificateId, @event, _verifierFixture.IssuerKey);

        var status = await Client.GetStatus(transaction);
        status.Status.Should().Be(Registry.V1.TransactionState.Unknown);

        await Client.SendTransactions(transaction);
        status = await Client.GetStatus(transaction);
        status.Status.Should().Be(Registry.V1.TransactionState.Pending);

        status = await Helper.RepeatUntilOrTimeout(
            () => Client.GetStatus(transaction),
            result => result.Status == Registry.V1.TransactionState.Committed,
            TimeSpan.FromSeconds(60));

        status.Message.Should().BeEmpty();

        var stream = await Client.GetStream(certId);
        stream.Transactions.Should().HaveCount(1);
    }
}