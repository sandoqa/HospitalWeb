using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;


// ===============================================
// Disable File Watcher for Render BEFORE Builder
// ===============================================

Environment.SetEnvironmentVariable(
    "DOTNET_USE_POLLING_FILE_WATCHER",
    "0"
);


// ===============================================
// Builder
// ===============================================

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Production,
    ContentRootPath = Directory.GetCurrentDirectory()
});


// ===============================================
// Configuration
// ===============================================

builder.Configuration.Sources.Clear();

builder.Configuration.AddJsonFile(
    "appsettings.json",
    optional: false,
    reloadOnChange: false
);


// ===============================================
// Render Port
// ===============================================

var port =
    Environment.GetEnvironmentVariable("PORT")
    ?? "10000";


builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);


// ===============================================
// MVC
// ===============================================

builder.Services.AddControllersWithViews();


// ===============================================
// Database
// ===============================================

var appData =
    Path.Combine(
        Directory.GetCurrentDirectory(),
        "App_Data"
    );


Directory.CreateDirectory(appData);


var dbPath =
    Path.Combine(
        appData,
        "hospital.db"
    );


// ===============================================
// Logs
// ===============================================

Console.WriteLine("====================================");

Console.WriteLine(
    $"Environment = {builder.Environment.EnvironmentName}"
);

Console.WriteLine(
    $"DB Path = {dbPath}"
);

Console.WriteLine(
    $"DB Exists = {File.Exists(dbPath)}"
);

if (File.Exists(dbPath))
{
    Console.WriteLine(
        $"DB Size = {new FileInfo(dbPath).Length}"
    );
}

Console.WriteLine(
    $"PORT = {port}"
);

Console.WriteLine("====================================");


// ===============================================
// SQLite
// ===============================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    });


// Import
builder.Services.AddScoped<AccessImporter>();


// ===============================================
// Build
// ===============================================

var app = builder.Build();


// ===============================================
// Database Test
// ===============================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db =
            scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();


        Console.WriteLine(
            "Database Connected = " +
            db.Database.CanConnect()
        );


        Console.WriteLine(
            "Doctors Count = " +
            db.Doctors.Count()
        );


        Console.WriteLine(
            "Training Count = " +
            db.TrainingRotations.Count()
        );

    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE ERROR = " + ex.Message
        );
    }
}



// ===============================================
// Middleware
// ===============================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}


app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


// ===============================================
// Route
// ===============================================

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Doctors}/{action=Index}/{id?}"
);


app.Run();