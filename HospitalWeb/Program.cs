using HospitalWeb.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// ==================================================
// MVC
// ==================================================

builder.Services.AddControllersWithViews();


// ==================================================
//  ÕœÌœ ﬁ«⁄œ… «·»Ì«‰« 
// ==================================================

string dbPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data",
    "hospital.db"
);


// ≈‰‘«¡ „Ã·œ App_Data

string? dbFolder = Path.GetDirectoryName(dbPath);

if (!string.IsNullOrEmpty(dbFolder))
{
    Directory.CreateDirectory(dbFolder);
}


// ==================================================
// „⁄·Ê„«  ﬁ«⁄œ… «·»Ì«‰« 
// ==================================================

Console.WriteLine("====================================");

Console.WriteLine(
    "Environment  = " +
    builder.Environment.EnvironmentName
);

Console.WriteLine(
    "Content Root = " +
    Directory.GetCurrentDirectory()
);

Console.WriteLine(
    "DB Path      = " +
    dbPath
);

Console.WriteLine(
    "DB Full Path = " +
    Path.GetFullPath(dbPath)
);

Console.WriteLine(
    "DB Exists    = " +
    File.Exists(dbPath)
);


if (File.Exists(dbPath))
{
    FileInfo info = new FileInfo(dbPath);

    Console.WriteLine(
        "DB Size      = " +
        info.Length +
        " bytes"
    );
}

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
// Build App
// ==================================================

var app = builder.Build();


// ==================================================
// ›Õ’ ﬁ«⁄œ… «·»Ì«‰« 
// ==================================================

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    try
    {
        bool connected = db.Database.CanConnect();

        Console.WriteLine(
            "Database Connected = " + connected
        );


        if (connected)
        {
            int doctorsCount = db.Doctors.Count();

            int trainingCount = db.TrainingRotations.Count();


            Console.WriteLine(
                "Doctors Count = " + doctorsCount
            );


            Console.WriteLine(
                "Training Count = " + trainingCount
            );


            File.WriteAllText(
                Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "db_result.txt"
                ),
                "DB Path = " + dbPath + Environment.NewLine +
                "DB Size = " + new FileInfo(dbPath).Length + Environment.NewLine +
                "Doctors Count = " + doctorsCount + Environment.NewLine +
                "Training Count = " + trainingCount
            );
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "Database ERROR = " + ex.Message
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
// HTTPS
// ==================================================

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}


// ==================================================
// Middleware
// ==================================================

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();


// ==================================================
// Default Route
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