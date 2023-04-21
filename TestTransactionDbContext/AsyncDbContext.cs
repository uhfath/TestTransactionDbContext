using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestTransactionDbContext
{
	internal class AsyncDbContext : DbContext
	{
		public DbSet<Client> Clients { get; protected set; }

		public AsyncDbContext(DbContextOptions<AsyncDbContext> options)
			: base(options)
		{
		}
	}
}
