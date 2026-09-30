using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;


// =====================================
// Render file watcher fix
// =====================================

Environment.SetEnvironmentVariable(
    "DOTNET_USE_POLLING_FILE_WATCHER",
    "true"
);


// =====================================
// Web options
// =====================================

var options = new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Production,
    ContentRootPath = Directory.GetCurrentDirectory()
};


var builder = WebApplication.CreateBuilder(options);


// =====================================
// Configuration
// =====================================

builder.Configuration.Sources.Clear();

builder.Configuration.AddJsonFile(
    "appsettings.json",
    optional: false,
    reloadOnChange: false
);


// =====================================
// PORT
// =====================================

var port =
    Environment.GetEnvironmentVariable("PORT")
    ?? "5000";


builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);


// =====================================
// MVC
// =====================================

builder.Services.AddControllersWithViews();


// =====================================
// Neon PostgreSQL
// =====================================

var connectionString =
    Environment.GetEnvironmentVariable(
        "ConnectionStrings__DefaultConnection"
    )
    ??
    builder.Configuration
        .GetConnectionString("DefaultConnection");



Console.WriteLine("====================================");
Console.WriteLine("Database Provider = PostgreSQL / Neon");
Console.WriteLine(
    "Connection String Configured = "
    + (!string.IsNullOrEmpty(connectionString))
);
Console.WriteLine("PORT = " + port);
Console.WriteLine("====================================");



builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseNpgsql(connectionString);
    }
);


// =====================================
// Access Importer
// =====================================

builder.Services.AddScoped<AccessImporter>();


// =====================================
// Build
// =====================================

var app = builder.Build();


// =====================================
// Database test
// =====================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db =
            scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();

        Console.WriteLine(
            "Database Connected = "
            + db.Database.CanConnect()
        );

        Console.WriteLine(
            "Doctors Count = "
            + db.Doctors.Count()
        );

        Console.WriteLine(
            "Training Count = "
            + db.TrainingRotations.Count()
        );

        Console.WriteLine(
            "Departments Count = "
            + db.Departments.Count()
        );
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE ERROR = "
            + ex.Message
        );
    }
}


// =====================================
// Error handling
// =====================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


// =====================================
// Middleware
// =====================================

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


// Sync API

app.MapControllers();


// MVC

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Doctors}/{action=Index}/{id?}"
);


app.Run();