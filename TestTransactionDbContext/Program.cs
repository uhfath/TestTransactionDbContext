using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Transactions;

namespace TestTransactionDbContext
{
	internal static class Program
	{
		private static TransactionScope CreateTransaction() =>
			new TransactionScope(
				TransactionScopeOption.Required,
				new TransactionOptions
				{
					IsolationLevel = IsolationLevel.ReadCommitted
				},
				TransactionScopeAsyncFlowOption.Enabled);

		private static TransactionScope CreateTransaction(DependentTransaction dependentTransaction) =>
			new TransactionScope(
				dependentTransaction,
				TransactionScopeAsyncFlowOption.Enabled);

		private static async Task Migrate(IServiceProvider serviceProvider)
		{
			await using (var scope = serviceProvider.CreateAsyncScope())
			{
				var dbContext = scope.ServiceProvider.GetRequiredService<AsyncDbContext>();
				await dbContext.Database.EnsureDeletedAsync();
				await dbContext.Database.EnsureCreatedAsync();
			}
		}

		private static async Task<Client> Create(IServiceProvider serviceProvider)
		{
			var client = new Client { Name = "client_1" };

			await using var scope = serviceProvider.CreateAsyncScope();
			var dbContext = scope.ServiceProvider.GetRequiredService<AsyncDbContext>();
			dbContext.Clients.Add(client);
			await dbContext.SaveChangesAsync();

			await Console.Out.WriteLineAsync($"Client: {client.Id}");
			return client;
		}

		private static Random _randomer = new();
		private static async Task Read(IServiceProvider serviceProvider, DependentTransaction dependentTransaction, int clientId, int index)
		{
			using (dependentTransaction)
			{
				//using (var transaction = CreateTransaction(dependentTransaction))
				{
					await using (var scope = serviceProvider.CreateAsyncScope())
					{
						var dbContext = scope.ServiceProvider.GetRequiredService<AsyncDbContext>();
						await Task.Delay(_randomer.Next(100, 250));
						var client = await dbContext.Clients.AsNoTracking().Where(cl => cl.Id == clientId).SingleOrDefaultAsync();
						await Console.Out.WriteLineAsync($"Read: {index} -> {client?.Id.ToString() ?? "!! NOT FOUND !!"}");
						//transaction.Complete();
					}
				}

				dependentTransaction.Complete();
			}
		}

		private static async Task Main()
		{
			using var serviceProvider = new ServiceCollection()
				.AddDbContext<AsyncDbContext>(opts =>
					//opts.UseNpgsql("Host=localhost;Database=AsyncDbTest;Username=postgres;Password=123;"))
					opts.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=AsyncDbTest;Trusted_Connection=True"))
				.BuildServiceProvider(true)
			;

			await Migrate(serviceProvider);
			using (var transaction = CreateTransaction())
			{
				var client = await Create(serviceProvider);
				var readTasks = Enumerable.Range(0, 100)
					.Select(v => new
					{
						Index = v,
						Dependent = Transaction.Current.DependentClone(DependentCloneOption.BlockCommitUntilComplete),
					})
					.Select(v => Read(serviceProvider, v.Dependent, client.Id, v.Index))
					.ToArray()
				;

				await Task.WhenAll(readTasks);
			}

			Console.ReadLine();
		}
	}
}