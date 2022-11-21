using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using NSec.Cryptography;
using ProjectOrigin.Electricity.Models;
using ProjectOrigin.PedersenCommitment;
using ProjectOrigin.Register.LineProcessor.Models;

namespace ProjectOrigin.Electricity.Tests;

internal static class ModelToProtoExtensions
{
    internal static Register.V1.Uuid ToProto(this Guid allocationId)
    {
        return new Register.V1.Uuid()
        {
            Value = allocationId.ToString()
        };
    }

    public static V1.Commitment ToProto(this Commitment obj)
    {
        return new V1.Commitment()
        {
            C = ByteString.CopyFrom(obj.C.ToByteArray())
        };
    }

    public static V1.CommitmentProof ToProto(this CommitmentParameters obj)
    {
        return new V1.CommitmentProof()
        {
            M = (ulong)obj.m,
            R = ByteString.CopyFrom(obj.r.ToByteArray())
        };
    }

    internal static V1.PublicKey ToProto(this PublicKey publicKey)
    {
        var bytes = publicKey.Export(KeyBlobFormat.RawPublicKey);
        return new V1.PublicKey()
        {
            Content = ByteString.CopyFrom(bytes)
        };
    }

    internal static V1.TimePeriod ToProto(this TimePeriod model)
    {
        return new V1.TimePeriod()
        {
            DateTimeFrom = Timestamp.FromDateTimeOffset(model.DateTimeFrom),
            DateTimeTo = Timestamp.FromDateTimeOffset(model.DateTimeTo),
        };
    }

    internal static Register.V1.FederatedStreamId ToProto(this FederatedStreamId id)
    {
        return new Register.V1.FederatedStreamId()
        {
            Registry = id.Registry,
            StreamId = new Register.V1.Uuid()
            {
                Value = id.StreamId.ToString()
            }
        };
    }
}
