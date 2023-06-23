using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Google.Protobuf;
using ProjectOrigin.Electricity.Consumption.Verifiers;
using ProjectOrigin.HierarchicalDeterministicKeys;
using ProjectOrigin.PedersenCommitment;
using Xunit;

namespace ProjectOrigin.Electricity.Tests;

public class ConsumptionSlicedVerifierTests
{
    private ConsumptionSlicedVerifier _verifier;

    public ConsumptionSlicedVerifierTests()
    {
        _verifier = new ConsumptionSlicedVerifier();
    }

    [Fact]
    public async Task ConsumptionSlicedEventVerifier_TransferCertificate_Valid()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);

        var @event = FakeRegister.CreateSliceEvent(cert.Id, sourceParams, 150, ownerKey.PublicKey);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, cert, @event);

        result.AssertValid();
    }

    [Fact]
    public async Task ConsumptionSlicedEventVerifier_NoCertificate_Invalid()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);

        var @event = FakeRegister.CreateSliceEvent(cert.Id, sourceParams, 150, ownerKey.PublicKey);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, null, @event);

        result.AssertInvalid("Certificate does not exist");
    }

    [Fact]
    public async Task ConsumptionSlicedEventVerifier_FakeSlice_SliceNotFound()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var newOwnerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);

        var fakeSliceParams = new SecretCommitmentInfo(250);
        var @event = FakeRegister.CreateSliceEvent(cert.Id, fakeSliceParams, 150, ownerKey.PublicKey);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, cert, @event);

        result.AssertInvalid("Slice not found");
    }

    [Fact]
    public async Task ConsumptionSlicedEventVerifier_WrongKey_InvalidSignature()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var otherKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);

        var @event = FakeRegister.CreateSliceEvent(cert.Id, sourceParams, 150, otherKey.PublicKey);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, otherKey);

        var result = await _verifier.Verify(transaction, cert, @event);

        result.AssertInvalid("Invalid signature for slice");
    }

    [Fact]
    public async Task ConsumptionSlicedEventVerifier_InvalidSlicePublicKey_InvalidFormat()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);

        var randomOwnerKeyData = new V1.PublicKey
        {
            Content = Google.Protobuf.ByteString.CopyFrom(new Fixture().Create<byte[]>())
        };

        var @event = FakeRegister.CreateSliceEvent(cert.Id, sourceParams, 150, ownerKey.PublicKey, randomOwnerKeyData);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, cert, @event);

        result.AssertInvalid("Invalid NewOwner key, not a valid publicKey");
    }

    [Fact]
    public async Task ConsumptionSlicedEventVerifier_InvalidSumProof_Invalid()
    {
        var ownerKey = Algorithms.Secp256k1.GenerateNewPrivateKey();
        var (cert, sourceParams) = FakeRegister.ConsumptionIssued(ownerKey.PublicKey, 250);

        var sumOverride = ByteString.CopyFrom(new Fixture().CreateMany<byte>(64).ToArray());

        var @event = FakeRegister.CreateSliceEvent(cert.Id, sourceParams, 150, ownerKey.PublicKey, sumOverride: sumOverride);
        var transaction = FakeRegister.SignTransaction(@event.CertificateId, @event, ownerKey);

        var result = await _verifier.Verify(transaction, cert, @event);

        result.AssertInvalid("Invalid sum proof");
    }
}
