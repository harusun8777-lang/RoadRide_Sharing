using System.Net.Http.Headers;
using Backend.Tests.TestDoubles;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Usecase.Reservation;
using Usecase.RideGroup;
using Usecase.User;

namespace Backend.Tests.Handler
{
    // DB を使わずに API を起動する。リポジトリとヘルスチェックは Fake に差し替える
    public class ApiFactory : WebApplicationFactory<Program>
    {
        public FakeUserRepository Users { get; } = new();
        public FakeReservationRepository Reservations { get; } = new();
        public FakeRideGroupRepository RideGroups { get; } = new();
        public FakeHealthCheck Health { get; } = new();

        public void ResetFakes()
        {
            Users.Reset();
            Reservations.Reset();
            RideGroups.Reset();
            Health.Reset();
        }

        // Authorization ヘッダー付きのクライアントを作る（token が null ならヘッダーなし）
        public HttpClient CreateClient(string? token)
        {
            var client = CreateClient();
            if (token is not null)
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return client;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Program.cs は Build() 前に Jwt セクションを読むため UseSetting で渡す
            builder.UseSetting("Jwt:Issuer", TestJwt.Issuer);
            builder.UseSetting("Jwt:Audience", TestJwt.Audience);
            builder.UseSetting("Jwt:SigningKey", TestJwt.SigningKey);
            builder.UseSetting("Jwt:ExpiresMinutes", TestJwt.ExpiresMinutes.ToString());
            builder.UseSetting("Database:EnsureCreatedOnStartup", "false");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.RemoveAll<IReservationRepository>();
                services.RemoveAll<IRideGroupRepository>();
                services.AddSingleton<IUserRepository>(Users);
                services.AddSingleton<IReservationRepository>(Reservations);
                services.AddSingleton<IRideGroupRepository>(RideGroups);

                // AddCheck<DatabaseHealthCheck> は型で登録されるため、登録の Factory を置き換える
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    var registration = options.Registrations.Single(r => r.Name == "database");
                    registration.Factory = _ => Health;
                });

                // DB に触れていないことを保証するため、DbContext の登録ごと消す
                services.RemoveAll<AppDbContext>();
                services.RemoveAll<DbContextOptions<AppDbContext>>();
            });
        }
    }
}
