using System.Threading.Tasks;
using Moq;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.Electricity.Interfaces;
using ProjectOrigin.Electricity.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class ConsumptionClaimedVerifierTests
{
    private ClaimedEventVerifier _verifier;
    private GranularCertificate? _otherCertificate;

    public ConsumptionClaimedVerifierTests()
    {
        var modelLoaderMock = new Mock<IRemoteModelLoader>();
        modelLoaderMock.Setup(obj => obj.GetModel<GranularCertificate>(It.IsAny<Common.V1.FederatedStreamId>()))
            .Returns(() => Task.FromResult(_otherCertificate));

        _verifier = new ClaimedEventVerifier(modelLoaderMock.Object);
    }

    [Fact]
    public async Task ProductionClaimedVerifier_Valid()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        consCert.Allocated(allocationId, prodCert, prodParams, consParams);
        prodCert.Claimed(allocationId);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task ProductionClaimedVerifier_Invalid_CertificateNotExists()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        consCert.Allocated(allocationId, prodCert, prodParams, consParams);
        prodCert.Claimed(allocationId);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Certificate does not exist");
    }

    [Fact]
    public async Task ProductionClaimedVerifier_Invalid_AllocationNotExist()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        prodCert.Claimed(allocationId);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("Allocation does not exist");
    }

    [Fact]
    public async Task ProductionClaimedVerifier_Invalid_InvalidSignature()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var otherKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        consCert.Allocated(allocationId, prodCert, prodParams, consParams);
        prodCert.Claimed(allocationId);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, otherKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("Invalid signature for slice");
    }

    [Fact]
    public async Task ProductionClaimedVerifier_Invalid_ConsumptionNotFound()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        consCert.Allocated(allocationId, prodCert, prodParams, consParams);
        prodCert.Claimed(allocationId);
        _otherCertificate = null;

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("ProductionCertificate does not exist");
    }

    [Fact]
    public async Task ProductionClaimedVerifier_Invalid_ConsumptionNotAllocated()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (consCert, consParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);
        var (prodCert, prodParams) = FakeRegister.ProductionIssued(ownerKey.PublicKey, 250);
        var allocationId = prodCert.Allocated(consCert, prodParams, consParams);
        consCert.Allocated(allocationId, prodCert, prodParams, consParams);
        _otherCertificate = prodCert;

        var @event = FakeRegister.CreateClaimedEvent(allocationId, consCert.Id);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, consCert, @event);

        result.AssertInvalid("Production not claimed");
    }
}
