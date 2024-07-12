using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

public class MatrixMultiplicationTest
{
    private const string baseUrl = "https://recruitment-test.investcloud.com/";

    private static readonly HttpClient client = new HttpClient();

    public static void Main(string[] args)
    {
        int size = 10; // Size of the matrices
        MatrixMultiplicationTest test = new MatrixMultiplicationTest();
        try
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            string passphrase = test.PerformTest(size).GetAwaiter().GetResult();

            stopwatch.Stop();
            Console.WriteLine($"Passphrase: {passphrase}");
            Console.WriteLine($"Total execution time: {stopwatch.ElapsedMilliseconds} ms");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
        Console.ReadLine();
    }

    public async Task<string> PerformTest(int size)
    {
        // Initialize matrices A and B
        int[,] A = await InitializeMatrix(size, "A");
        int[,] B = await InitializeMatrix(size, "B");

        // Perform matrix multiplication A x B
        int[,] C = MatrixMultiply(A, B);

        // Flatten matrix C into a concatenated string
        string concatenatedResult = FlattenMatrixToString(C);

        // Calculate MD5 hash of the concatenated string
        string md5Hash = CalculateMD5Hash(concatenatedResult);

        // Submit MD5 hash to validate and get passphrase
        string passphrase = await ValidateResult(md5Hash);

        return passphrase;
    }

    private async Task<int[,]> InitializeMatrix(int size, string dataset)
    {
        int[,] matrix = new int[size, size];

        // Fetch rows of matrix in parallel
        await Task.Run(async () =>
        {
            for (int i = 0; i < size; i++)
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync($"{baseUrl}api/numbers/{dataset}/row/{i}");
                    response.EnsureSuccessStatusCode(); // Ensure successful response
                    string responseJson = await response.Content.ReadAsStringAsync();
                    JObject responseObject = JObject.Parse(responseJson);

                    // Access the "Value" property and deserialize into int[]
                    int[] rowData = responseObject["Value"].ToObject<int[]>();

                    // Fill the matrix with rowData
                    for (int j = 0; j < size; j++)
                    {
                        matrix[i, j] = rowData[j];
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error fetching row {i} of {dataset} matrix: {ex.Message}");
                    throw; // Rethrow exception to stop loop if needed
                }
            }
        });

        return matrix;
    }

    private int[,] MatrixMultiply(int[,] A, int[,] B)
    {
        int size = A.GetLength(0);
        int[,] C = new int[size, size];

        // Perform matrix multiplication in parallel
        Parallel.For(0, size, i =>
        {
            for (int j = 0; j < size; j++)
            {
                C[i, j] = 0;
                for (int k = 0; k < size; k++)
                {
                    C[i, j] += A[i, k] * B[k, j];
                }
            }
        });

        return C;
    }

    private string FlattenMatrixToString(int[,] matrix)
    {
        StringBuilder sb = new StringBuilder();
        int size = matrix.GetLength(0);

        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                sb.Append(matrix[i, j].ToString());
            }
        }

        return sb.ToString();
    }

    private string CalculateMD5Hash(string input)
    {
        using (MD5 md5 = MD5.Create())
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            byte[] hashBytes = md5.ComputeHash(inputBytes);

            StringBuilder sb = new StringBuilder();
            foreach (byte b in hashBytes)
            {
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }

    private async Task<string> ValidateResult(string md5Hash)
    {
        using (var client = new HttpClient())
        {
            var content = new StringContent(md5Hash, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync($"{baseUrl}api/numbers/validate", content);

            if (response.IsSuccessStatusCode)
            {
                string passphrase = await response.Content.ReadAsStringAsync();
                return passphrase;
            }
            else
            {
                throw new Exception($"Failed to validate result. Status code: {response.StatusCode}");
            }
        }
    }
}
