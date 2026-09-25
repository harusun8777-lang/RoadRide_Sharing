using Handler.Auth;
using Handler.Reservations;
using Handler.Users;
using Infrastructure;
using Infrastructure.Auth;
using Infrastructure.Reservations;
using Infrastructure.RideGroups;
using Infrastructure.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Usecase.Auth;
using Usecase.Reservation;
using Usecase.RideGroup;
using Usecase.User;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ログイン時に自前で発行した JWT（HS256）を検証する。設定値が不正なら起動時に止める
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
jwtOptions.Validate();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = jwtOptions.CreateValidationParameters();
    });
builder.Services.AddAuthorization();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure()));

builder.Services.AddScoped<IReservationRepository, EfReservationRepository>();
builder.Services.AddScoped<RegisterReservationUseCase>();
builder.Services.AddScoped<ListReservationsUseCase>();
builder.Services.AddScoped<GetReservationUseCase>();
builder.Services.AddScoped<CancelReservationUseCase>();

builder.Services.AddScoped<IRideGroupRepository, EfRideGroupRepository>();

builder.Services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddScoped<LoginUseCase>();

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<RegisterUserUseCase>();
builder.Services.AddScoped<GetCurrentUserUseCase>();
builder.Services.AddScoped<AddUserRoleUseCase>();
builder.Services.AddScoped<SwitchUserRoleUseCase>();

var app = builder.Build();

// マイグレーション導入までは起動時にスキーマを作成する（DBが無ければ作成、あれば何もしない）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

var apiGroup = app.MapGroup("/api").RequireAuthorization();
apiGroup.MapAuthEndpoints();
apiGroup.MapReservationEndpoints();
apiGroup.MapUserEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast =  Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
