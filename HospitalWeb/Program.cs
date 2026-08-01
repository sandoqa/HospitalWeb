using HospitalWeb.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


// ==================================================
// MVC
// ==================================================

builder.Services.AddControllersWithViews();



// ==================================================
//  ÕœÌœ ﬁ«⁄œ… «·»Ì«‰«  („”«— À«» )
// ==================================================

string dbPath = Path.Combine(
    Directory.GetCurrentDirectory(),
    "App_Data",
    "hospital_backup_447.db"
);


// ≈‰‘«¡ „Ã·œ App_Data ≈–« €Ì— „ÊÃÊœ

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
Console.WriteLine("DB Full Path = " + Path.GetFullPath(dbPath));

Console.WriteLine("Current Directory = " + Directory.GetCurrentDirectory());

Console.WriteLine("App_Data Exists = " +
    File.Exists(Path.Combine(
        Directory.GetCurrentDirectory(),
        "App_Data",
        "hospital.db")));

Console.WriteLine(
    "DB Exists    = " +
    File.Exists(dbPath)
);


// Õ›Ÿ „⁄·Ê„«  ﬁ«⁄œ… «·»Ì«‰«  ›Ì „·› „ƒﬁ 
File.WriteAllText(
    Path.Combine(
        Directory.GetCurrentDirectory(),
        "db_info.txt"
    ),
    $"DB Path = {dbPath}\n" +
    $"DB Exists = {File.Exists(dbPath)}\n" +
    $"DB Size = {(File.Exists(dbPath) ? new FileInfo(dbPath).Length : 0)} bytes\n" +
    $"Doctors Count = {(File.Exists(dbPath) ? "Check after connection" : "No DB")}"
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





var app = builder.Build();


using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    Console.WriteLine("Current DB Doctors = " + db.Doctors.Count());
}


// ==================================================
// «Œ »«— «·« ’«· »ﬁ«⁄œ… «·»Ì«‰« 
// ==================================================

using (var scope = app.Services.CreateScope())
{
    var db =
        scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();


    bool connected =
        db.Database.CanConnect();


    Console.WriteLine(
        "Database Connected = " +
        connected
    );


    if (connected)
    {
        int doctorsCount = db.Doctors.Count();

        int trainingCount = db.TrainingRotations.Count();


        Console.WriteLine(
            "Doctors Count = " +
            doctorsCount
        );


        Console.WriteLine(
            "Training Count = " +
            trainingCount
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


// ===============================================
// ›Õ’ ﬁÊ«⁄œ «Õ Ì«ÿÌ… (··„ﬁ«—‰… ›ﬁÿ)
// ===============================================

void CheckBackupDatabase(string path, string name)
{
    try
    {
        if (!File.Exists(path))
        {
            Console.WriteLine(name + " not found");
            return;
        }


        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();

            var cmd = conn.CreateCommand();

            cmd.CommandText = "SELECT COUNT(*) FROM Doctors";


            var count = cmd.ExecuteScalar();


            Console.WriteLine(
                name + " Doctors = " + count
            );
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            name + " ERROR = " + ex.Message
        );
    }
}



// ›Õ’ «·„·›«  «·ﬁœÌ„…
CheckBackupDatabase(
    @"C:\Users\Mahmoud\Desktop\m12\HospitalWeb\old_correct_hospital.db",
    "old_correct_hospital"
);


CheckBackupDatabase(
    @"C:\Users\Mahmoud\Desktop\m12\HospitalWeb\old_initial_hospital.db",
    "old_initial_hospital"
);


app.Run();