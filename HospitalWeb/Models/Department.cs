namespace HospitalWeb.Models
{
    public class Department
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public List<TrainingRotation> TrainingRotations { get; set; } = new();
    }
}