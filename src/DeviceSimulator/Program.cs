using System.Net.Http.Json;

var http = new HttpClient();

http.DefaultRequestHeaders.Add("x-api-key", "local-dev");

var random = new Random();
var deviceId = "sim-01";
var patientId = "p-123";

while (true)
{
    var hr = new
    {
        MeasurementId = Guid.NewGuid(),
        Timestamp = DateTimeOffset.UtcNow,
        DeviceId = deviceId,
        PatientId = patientId,
        Type = "HeartRate",
        Value = random.Next(58, 98),
        Unit = "bpm"
    };

    await http.PostAsJsonAsync("https://localhost:7296/api/v1/measurements", hr);

    await Task.Delay(TimeSpan.FromSeconds(2));
}