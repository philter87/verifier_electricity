using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AutoFixture;
using Microsoft.Extensions.Options;
using Moq;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Options;
using ProjectOrigin.Electricity.Services;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class ConsumptionIssuedVerifierTests
{
    const string IssuerArea = "DK1";
    private IPrivateKey _issuerKey;
    private IssuedEventVerifier _verifier;

    public ConsumptionIssuedVerifierTests()
    {
        _issuerKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var optionsMock = new Mock<IOptionsMonitor<NetworkOptions>>();
        optionsMock.Setup(obj => obj.CurrentValue).Returns(new NetworkOptions()
        {
            Registries = new Dictionary<string, RegistryInfo>(),
            Areas = new Dictionary<string, AreaInfo>(){
                {IssuerArea, new AreaInfo(){
                    IssuerKeys = new List<KeyInfo>(){
                        new KeyInfo(){
                            PublicKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(_issuerKey.PublicKey.ExportPkixText()))
                        }
                    }
                }}
            },
        });
        var issuerService = new GridAreaIssuerOptionsService(optionsMock.Object);

        _verifier = new IssuedEventVerifier(issuerService);
    }

    [Fact]
    public async Task ConsumptionIssuedVerifier_IssueCertificate_Success()
    {
        var @event = FakeRegister.CreateConsumptionIssuedEvent();
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task ConsumptionIssuedVerifier_CertificateExists_Fail()
    {
        var @event = FakeRegister.CreateConsumptionIssuedEvent();
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);
        var certificate = new GranularCertificate(@event);

        var result = await _verifier.Verify(transaction, certificate, @event);

        result.AssertInvalid($"Certificate with id ”{@event.CertificateId.StreamId}” already exists");
    }

    [Fact]
    public async Task ConsumptionIssuedVerifier_QuantityCommitmentInvalid_Fail()
    {
        var @event = FakeRegister.CreateConsumptionIssuedEvent(quantityCommitmentOverride: FakeRegister.InvalidCommitment());
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid range proof for Quantity commitment");
    }

    [Fact]
    public async Task ConsumptionIssuedVerifier_InvalidOwner_Fail()
    {
        var randomOwnerKeyData = new V1.PublicKey
        {
            Content = Google.Protobuf.ByteString.CopyFrom(new Fixture().Create<byte[]>())
        };

        var @event = FakeRegister.CreateConsumptionIssuedEvent(ownerKeyOverride: randomOwnerKeyData);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, _issuerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid owner key, not a valid publicKey");
    }

    [Fact]
    public async Task ConsumptionIssuedVerifier_InvalidSignature_Fail()
    {
        var someOtherKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var @event = FakeRegister.CreateConsumptionIssuedEvent();
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, someOtherKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Invalid issuer signature for GridArea ”DK1”");
    }

    [Fact]
    public async Task ConsumptionIssuedVerifier_NoIssuerForArea_Fail()
    {
        var someOtherKey = Algorithms.Ed25519.GenerateNewPrivateKey();

        var @event = FakeRegister.CreateConsumptionIssuedEvent(gridAreaOverride: "DK2");
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, someOtherKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("No issuer found for GridArea ”DK2”");
    }
}
