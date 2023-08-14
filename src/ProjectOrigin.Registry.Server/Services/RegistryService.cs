using Grpc.Core;
using MassTransit;
using ProjectOrigin.Registry.V1;
using ProjectOrigin.VerifiableEventStore.Services.EventStore;
using System.Threading.Tasks;
using System.Linq;
using System;
using ProjectOrigin.VerifiableEventStore.Services.TransactionStatusCache;
using System.Security.Cryptography;
using Google.Protobuf;
using System.Diagnostics.Metrics;

namespace ProjectOrigin.Registry.Server;

public class RegistryService : V1.RegistryService.RegistryServiceBase
{
    public static Meter Meter = new("Registry.RegistryService");
    public static Counter<long> TransactionsSubmitted = Meter.CreateCounter<long>("TransactionsSubmitted");

    private IEventStore _eventStore;
    private IBus _bus;
    private ITransactionStatusService _transactionStatusService;

    public RegistryService(IEventStore eventStore, IBus bus, ITransactionStatusService transactionStatusService)
    {
        _eventStore = eventStore;
        _bus = bus;
        _transactionStatusService = transactionStatusService;
    }

    public override async Task<SubmitTransactionResponse> SendTransactions(SendTransactionsRequest request, ServerCallContext context)
    {
        var jobs = request.Transactions.Select(TransactionJob.Create).ToList();

        foreach (var j in jobs)
        {
            await _bus.Publish(j);
        }

        request.Transactions.AsParallel().ForAll(async transaction =>
        {
            var transactionHash = Convert.ToBase64String(SHA256.HashData(transaction.ToByteArray()));
            await _transactionStatusService.SetTransactionStatus(
                transactionHash,
                new VerifiableEventStore.Models.TransactionStatusRecord(VerifiableEventStore.Models.TransactionStatus.Pending)
                );
        });

        TransactionsSubmitted.Add(request.Transactions.Count);

        return new SubmitTransactionResponse();
    }

    public override async Task<GetTransactionStatusResponse> GetTransactionStatus(GetTransactionStatusRequest request, ServerCallContext context)
    {
        var state = await _transactionStatusService.GetTransactionStatus(request.Id);
        return new GetTransactionStatusResponse
        {
            Status = (V1.TransactionState)state.NewStatus,
            Message = state.Message,
        };
    }

    public async override Task<GetStreamTransactionsResponse> GetStreamTransactions(V1.GetStreamTransactionsRequest request, ServerCallContext context)
    {
        var streamId = Guid.Parse(request.StreamId.Value);
        var verifiableEvents = await _eventStore.GetEventsForEventStream(streamId);
        var transactions = verifiableEvents.Select(x => V1.Transaction.Parser.ParseFrom(x.Content));

        var response = new GetStreamTransactionsResponse();
        response.Transactions.AddRange(transactions);

        return response;
    }
}