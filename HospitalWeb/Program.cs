using Microsoft.EntityFrameworkCore;
using HospitalWeb.Data;

var builder = WebApplication.CreateBuilder(args);


// ===============================
// MVC
// ===============================
builder.Services.AddControllersWithViews();



// ===============================
// «·»ÕÀ ⁄‰ ﬁ«⁄œ… «·»Ì«‰« 
// ===============================

string[] databasePaths =
{
    Path.Combine(
        Directory.GetCurrentDirectory(),
        "App_Data",
        "hospital.db"
    ),

    Path.Combine(
        Directory.GetCurrentDirectory(),
        "hospital.db"
    )
};


string dbPath =
    databasePaths.FirstOrDefault(File.Exists)
    ??
    databasePaths[0];



string? dbFolder =
    Path.GetDirectoryName(dbPath);


if (!string.IsNullOrEmpty(dbFolder))
{
    Directory.CreateDirectory(dbFolder);
}




// ===============================
// „⁄·Ê„«  ﬁ«⁄œ… «·»Ì«‰« 
// ===============================

Console.WriteLine("====================================");
Console.WriteLine("Environment  = "
    + builder.Environment.EnvironmentName);

Console.WriteLine("Content Root = "
    + Directory.GetCurrentDirectory());

Console.WriteLine("DB Path      = "
    + dbPath);

Console.WriteLine("DB Exists    = "
    + File.Exists(dbPath));


if (File.Exists(dbPath))
{
    var info = new FileInfo(dbPath);

    Console.WriteLine(
        "DB Size      = "
        + info.Length
        + " bytes"
    );
}

Console.WriteLine("====================================");




// ===============================
// SQLite
// ===============================

builder.Services.AddDbContext<ApplicationDbContext>(
    options =>
    {
        options.UseSqlite(
            $"Data Source={dbPath}"
        );
    });




// ===============================
// Access Importer
// ===============================

builder.Services.AddScoped<AccessImporter>();




var app = builder.Build();




// ===============================
// «Œ »«— «·« ’«·
// ===============================

using (var scope = app.Services.CreateScope())
{
    try
    {
        var db =
            scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();


        bool connected =
            db.Database.CanConnect();


        Console.WriteLine(
            "Database Connected = "
            + connected
        );


        if (connected)
        {
            int doctors =
                db.Doctors.Count();


            Console.WriteLine(
                "Doctors Count = "
                + doctors
            );
        }
        else
        {
            Console.WriteLine(
                "Database connection failed"
            );
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine(
            "DATABASE ERROR"
        );

        Console.WriteLine(
            ex.ToString()
        );
    }
}






// ===============================
// Error Handling
// ===============================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}




// ===============================
// Middleware
// ===============================

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();




// ===============================
// Default Route
// ===============================

app.MapControllerRoute(
    name: "default",
    pattern:
    "{controller=Doctors}/{action=Index}/{id?}"
);



app.Run();