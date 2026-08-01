using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;


// ==================================================
// Builder - Render Production
// ==================================================

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    EnvironmentName = Environments.Production
});


// ==================================================
// Render Port Fix
// ==================================================

var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";

builder.WebHost.UseUrls(
    $"http://0.0.0.0:{port}"
);


// ==================================================
//  ⁄ÿÌ· „—«ﬁ»… «·„·›«  ›Ì Render
// ==================================================

builder.Configuration.Sources.Clear();

builder.Configuration
    .AddJsonFile(
        "appsettings.json",
        optional: false,
        reloadOnChange: false
    );


// ==================================================
// MVC
// ==================================================

builder.Services.AddControllersWithViews();


// ==================================================
// Database Path
// ==================================================

string appDataFolder = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data"
);


Directory.CreateDirectory(appDataFolder);



string dbPath = Path.Combine(
    appDataFolder,
    "hospital.db"
);



// ==================================================
// Database Information
// ==================================================

Console.WriteLine("====================================");

Console.WriteLine(
    "Environment = " +
    builder.Environment.EnvironmentName
);

Console.WriteLine(
    "Content Root = " +
    Directory.GetCurrentDirectory()
);

Console.WriteLine(
    "DB Path = " +
    dbPath
);

Console.WriteLine(
    "DB Exists = " +
    File.Exists(dbPath)
);


if (File.Exists(dbPath))
{
    Console.WriteLine(
        "DB Size = " +
        new FileInfo(dbPath).Length +
        " bytes"
    );
}


Console.WriteLine(
    "PORT = " + port
);

Console.WriteLine("====================================");



// ==================================================
// SQLite
// ==================================================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    });



// ==================================================
// Access Importer
// ==================================================

builder.Services.AddScoped<AccessImporter>();



// ==================================================
// Build
// ==================================================

var app = builder.Build();



// ==================================================
// Database Test
// ==================================================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();


        bool connected = db.Database.CanConnect();


        Console.WriteLine(
            "Database Connected = " + connected
        );


        if (connected)
        {
            int doctors =
                db.Doctors.Count();


            int training =
                db.TrainingRotations.Count();



            Console.WriteLine(
                "Doctors Count = " +
                doctors
            );


            Console.WriteLine(
                "Training Count = " +
                training
            );


            File.WriteAllText(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "db_result.txt"
                ),
                "DB Path = " + dbPath + Environment.NewLine +
                "DB Size = " + new FileInfo(dbPath).Length + Environment.NewLine +
                "Doctors Count = " + doctors + Environment.NewLine +
                "Training Count = " + training
            );
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE ERROR = " +
            ex.Message
        );
    }
}



// ==================================================
// Error Handling
// ==================================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}



// ==================================================
// Middleware
// ==================================================

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();



// ==================================================
// Route
// ==================================================

app.MapControllerRoute(
    name: "default",
    pattern:
    "{controller=Doctors}/{action=Index}/{id?}"
);



// ==================================================
// Run
// ==================================================

app.Run();