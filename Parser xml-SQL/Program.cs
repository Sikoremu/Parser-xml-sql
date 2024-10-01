using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;

class Program
{
    static string inputDirectory = @"C:\Users\kasik\OneDrive - KK-Group\Skrivebord\KASIK\SATE FRY\XML";
    static string outputDirectory = @"C:\Users\kasik\OneDrive - KK-Group\Skrivebord\KASIK\SATE FRY\Wyniki";

/*********************************************************************************
* @brief Monitors a specified directory for newly created XML files and processes them.
* This method sets up a `FileSystemWatcher` to monitor the `inputDirectory` for new XML files. 
* When a new file is detected, it attempts to process the file by calling `ProcessXmlFile`.
* @return None.
*********************************************************************************/
    static void Main()
    {
        using (FileSystemWatcher watcher = new FileSystemWatcher())
        {
            watcher.Path = inputDirectory;
            watcher.Filter = "*.xml";
            watcher.NotifyFilter = NotifyFilters.FileName;
            watcher.IncludeSubdirectories = false;

            int processedFiles = 0;

            watcher.Created += (sender, e) =>
            {
                while (true)
                {
                    try
                    {
                        string xmlFilePath = e.FullPath;
                        Console.WriteLine($"Processing file: {xmlFilePath}");
                        using (FileStream fs = File.Open(xmlFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            ProcessXmlFile(xmlFilePath);
                            processedFiles++;
                            break;
                        }
                    }
                    catch (IOException ex) when (ex.Message.Contains("being used by another process"))
                    {
                        Console.WriteLine("File is currently in use. Waiting for 5 seconds...");
                        Thread.Sleep(5000);
                    }
                }
            };

            Console.WriteLine($"Total processed files: {processedFiles}");

            watcher.EnableRaisingEvents = true;

            Console.WriteLine("Directory monitoring started. Waiting for new XML files...");
            Console.ReadLine();
        }

        //Odczytywanie kodu kraju z nazwy 
    }

/*********************************************************************************
* @brief Processes an XML file by extracting data and inserting it into the database.
* This method loads the XML file, extracts relevant test data, and inserts this data 
* into multiple database tables using various helper methods.
* @param[in] xmlFilePath: The file path of the XML file to be processed.
* @return None.
*********************************************************************************/
    static void ProcessXmlFile(string xmlFilePath)
    {
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(xmlFilePath);
        string outputPath = Path.Combine(outputDirectory, fileNameWithoutExtension + ".txt");

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        XDocument xmlDoc = XDocument.Load(xmlFilePath);

        string testEndDate = GetTestEndDate(xmlDoc);
        string testStartDate = GetTestStartDate(xmlDoc);
        string Result = GetTestResult(xmlDoc);
        string testEndTime = GetTestEndTime(xmlDoc);
        string testStartTime = GetTestStartTime(xmlDoc);

        var callTestPrimitiveElements = GetCallTestPrimitiveElements(xmlDoc);

        try
        {
            string connectionString = "Data Source=LT-00812-18;Initial Catalog=QA_TEST;Integrated Security=true;";

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();

                int existingProductId = GetorCreateProduct(conn, xmlDoc);
                Console.WriteLine("Produkt ID: " + existingProductId);

                int existingFamilyId = GetorCreateProductFamily(conn, xmlDoc);
                Console.WriteLine("Rodzina Produktu ID: " + existingFamilyId);

                int existingCustomerId = GetorCreateCustomer(conn, xmlDoc);
                Console.WriteLine("Klient ID: " + existingCustomerId);

                int testId = InsertTestData(conn, existingFamilyId, existingProductId, existingCustomerId, xmlDoc, testStartDate, testStartTime, testEndDate, testEndTime, Result);
                Console.WriteLine("Test ID: " + testId);

                foreach (var element in callTestPrimitiveElements)
                {
                    int stepId = GetOrCreateTestStep(conn, element.NestedStepName);
                    var singleElementCollection = new List<dynamic> { element };
                    InsertTestStepsData(conn, testId, stepId, singleElementCollection);
                    Console.WriteLine("Step ID: " + stepId);
                }
            }
        }
        catch (SqlException ex)
        {
            Console.WriteLine("Error connecting to SQL Server: " + ex.Message);
        }
    }

/*********************************************************************************
* @brief Retrieves the value of a specified XML element.
* This method selects an XML element by its path and returns its value.
* @param[in] xmlDoc: The XML document to search.
* @param[in] elementPath: The XPath of the element whose value is to be retrieved.
* @return The value of the specified XML element, or "No data" if the element is not found.
*********************************************************************************/
    static string GetElementValue(XDocument xmlDoc, string elementPath)
    {
        XElement element = xmlDoc.XPathSelectElement("//*[normalize-space(@Name) = '" + elementPath + "']");
        return element != null ? element.Value : "No data";
    }

/*********************************************************************************
* @brief Retrieves the test end date from an XML document.
* This method extracts the year, month, and day of the test end date from the XML document.
* @param[in] xmlDoc: The XML document to search.
* @return The formatted test end date (YYYY-MM-DD), or "No data" if the date is not found.
*********************************************************************************/
    static string GetTestEndDate(XDocument xmlDoc)
    {
        XElement yearElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Year");
        XElement monthElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Month");
        XElement monthDayElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "MonthDay");

        if (yearElement != null && monthElement != null && monthDayElement != null)
        {
            string year = yearElement.Element("Value")?.Value;
            string month = monthElement.Element("Value")?.Value;
            string monthDay = monthDayElement.Element("Value")?.Value;

            if (year != null && month != null && monthDay != null)
            {
                return $"{year}-{month.PadLeft(2, '0')}-{monthDay.PadLeft(2, '0')}";
            }
        }
        return "No data";
    }

/*********************************************************************************
* @brief Retrieves the test end time from an XML document.
* This method extracts the hours, minutes, and seconds of the test end time from the XML document.
* @param[in] xmlDoc: The XML document to search.
* @return The formatted test end time (HH:MM:SS), or "No data" if the time is not found.
    *********************************************************************************/
    static string GetTestEndTime(XDocument xmlDoc)
    {
        XElement hoursElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Hours");
        XElement minutesElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Minutes");
        XElement secondsElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Seconds");

        if (hoursElement != null && minutesElement != null && secondsElement != null)
        {
            string hours = hoursElement.Element("Value")?.Value;
            string minutes = minutesElement.Element("Value")?.Value;
            string seconds = secondsElement.Element("Value")?.Value;

            if (hours != null && minutes != null && seconds != null)
            {
                return $"{hours.PadLeft(2, '0')}:{minutes.PadLeft(2, '0')}:{seconds.PadLeft(2, '0')}";
            }
        }
        return "No data";
    }

/*********************************************************************************
* @brief Retrieves the test start date from an XML document.
* This method extracts the year, month, and day of the test start date from the XML document.
* @param[in] xmlDoc: The XML document to search.
* @return The formatted test start date (YYYY-MM-DD), or "No data" if the date is not found.
*********************************************************************************/
    static string GetTestStartDate(XDocument xmlDoc)
    {
        XElement startDateElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "StartDate");

        if (startDateElement != null)
        {
            XElement yearElement = startDateElement.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Year");
            XElement monthElement = startDateElement.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Month");
            XElement monthDayElement = startDateElement.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "MonthDay");

            if (yearElement != null && monthElement != null && monthDayElement != null)
            {
                string year = yearElement.Element("Value")?.Value;
                string month = monthElement.Element("Value")?.Value;
                string monthDay = monthDayElement.Element("Value")?.Value;

                if (year != null && month != null && monthDay != null)
                {
                    return $"{year}-{month.PadLeft(2, '0')}-{monthDay.PadLeft(2, '0')}";
                }
            }
        }
        return "No data";
    }

/*********************************************************************************
* @brief Retrieves the test start time from an XML document.
* This method extracts the hours, minutes, and seconds of the test start time from the XML document.
* @param[in] xmlDoc: The XML document to search.
* @return The formatted test start time (HH:MM:SS), or "No data" if the time is not found.
*********************************************************************************/
    static string GetTestStartTime(XDocument xmlDoc)
    {
        XElement startTimeElement = xmlDoc.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "StartTime");

        if (startTimeElement != null)
        {
            XElement hoursElement = startTimeElement.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Hours");
            XElement minutesElement = startTimeElement.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Minutes");
            XElement secondsElement = startTimeElement.Descendants("Prop").FirstOrDefault(e => (string)e.Attribute("Name") == "Seconds");

            if (hoursElement != null && minutesElement != null && secondsElement != null)
            {
                string hours = hoursElement.Element("Value")?.Value;
                string minutes = minutesElement.Element("Value")?.Value;
                string seconds = secondsElement.Element("Value")?.Value;

                if (hours != null && minutes != null && seconds != null)
                {
                    return $"{hours.PadLeft(2, '0')}:{minutes.PadLeft(2, '0')}:{seconds.PadLeft(2, '0')}";
                }
            }
        }
        return "No data";
    }

/*********************************************************************************
* @brief Retrieves the test result from an XML document.
* This method extracts the test result from the XML document based on a specified report type.
* @param[in] xmlDoc: The XML document to search.
* @return The test result, or "No data" if the result is not found.
*********************************************************************************/
    static string GetTestResult(XDocument xmlDoc)
    {
        XElement reportElement = xmlDoc.Descendants("Report").FirstOrDefault(e => (string)e.Attribute("Type") == "UUT");

        if (reportElement != null)
        {
            XAttribute resultAttribute = reportElement.Attribute("UUTResult");
            if (resultAttribute != null)
            {
                return resultAttribute.Value;
            }
        }
        return "No data";
    }

    /*********************************************************************************
    * @brief Retrieves elements related to "Call Test Primitive" from an XML document.
    * This method extracts elements with specific attributes and values related to "Call Test Primitive" 
    * and returns them as a collection of dynamic objects.
    * @param[in] xmlDoc: The XML document to search.
    * @return A collection of dynamic objects containing test primitive data.
    *********************************************************************************/
    public static IEnumerable<dynamic> GetCallTestPrimitiveElements(XDocument xmlDoc)
    {
        var results = new List<dynamic>();

        // Pobierz wszystkie elementy "Prop", które mają atrybut "Name" równy "StepName" i wartość zaczynającą się od "Call Test Primitive ("
        var callTestPrimitives = xmlDoc.Descendants("Prop")
            .Where(e => e.Attribute("Name")?.Value == "StepName" && e.Value.StartsWith("Call Test Primitive ("))
            .ToList();

        foreach (var callTestPrimitive in callTestPrimitives)
        {
            var sequenceCall = callTestPrimitive.Parent.Elements("Prop")
                .FirstOrDefault(e => e.Attribute("Name")?.Value == "SequenceCall");

            if (sequenceCall != null)
            {
                var resultList = sequenceCall.Elements("Prop")
                    .FirstOrDefault(e => e.Attribute("Name")?.Value == "ResultList");

                if (resultList != null)
                {
                    var firstValue = resultList.Descendants("Value").FirstOrDefault();
                    if (firstValue != null)
                    {
                        var result = new
                        {
                            NestedStepName = GetPropValue(firstValue, "StepName"),
                            Status = GetPropValue(firstValue, "Status"),
                            TotalTime = GetPropValue(firstValue, "TotalTime"),
                            Numeric = GetPropValue(firstValue, "Numeric"),
                            Low = GetPropValue(firstValue, "Low"),
                            High = GetPropValue(firstValue, "High"),
                            NumLoops = GetPropValue(firstValue, "NumLoops"),
                            NumPassed = GetPropValue(firstValue, "NumPassed"),
                            NumFailed = GetPropValue(firstValue, "NumFailed"),
                        };

                        if (!string.IsNullOrEmpty(result.NestedStepName))
                        {
                            results.Add(result);
                        }
                    }
                }
            }
        }

        return results;
    }

/*********************************************************************************
* @brief Retrieves the value of a specified property from an XML element.
* This method searches for a property within an XML element and returns its value.
* @param[in] parent: The parent XML element to search within.
* @param[in] name: The name of the property to find.
* @return The value of the specified property, or null if the property is not found.
*********************************************************************************/
    private static string GetPropValue(XElement parent, string name)
    {
        var element = parent.Descendants("Prop").FirstOrDefault(prop => prop.Attribute("Name")?.Value == name);
        return element?.Value;
    }

/*********************************************************************************
* @brief Retrieves the nested step name from an XML element.
* This method extracts the nested step name from a series of XML elements.
* @param[in] element: The XML element from which to start the search.
* @return The nested step name, or null if not found.
*********************************************************************************/
    private static string GetNestedStepName(XElement element)
    {
        var nestedStepName = element.AncestorsAndSelf("Prop")
            .Where(prop => prop.Attribute("Name")?.Value == "TS")?
            .SelectMany(ts => ts.Descendants("Prop"))
            .Where(prop => !string.IsNullOrEmpty(prop.Value) &&
                           !prop.Value.StartsWith("NOTICE!!!"))
            .Where(prop => !prop.Value.StartsWith("Wait - "))
            .Where(prop => !prop.Value.StartsWith("*#*NOTE!"))
            .Where(prop => prop.Attribute("Name")?.Value == "StepName")
            .FirstOrDefault()?.Value;
    
        return nestedStepName;
    }

/*********************************************************************************
* @brief Retrieves or creates a product record in the database.
* This method checks if a product with the specified type exists in the database.
* If it exists, its ID is returned. If not, a new product is created, and its ID is returned.
* @param[in] conn: The SQL connection object.
* @param[in] xmlDoc: The XML document containing the product information.
* @return The ID of the existing or newly created product.
*********************************************************************************/
    static int GetorCreateProduct(SqlConnection conn, XDocument xmlDoc)
    {
        string productType = GetElementValue(xmlDoc, "ModelNumber");
        string sqlCheckProduct = "SELECT Product_ID FROM dbo.TestProducts WHERE Type = @Type";

        using (SqlCommand cmdCheck = new SqlCommand(sqlCheckProduct, conn))
        {
            cmdCheck.Parameters.AddWithValue("@Type", productType);
            object existingId = cmdCheck.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                return (int)existingId;
            }
            else
            {
                return InsertNewProduct(conn, xmlDoc);
            }
        }
    }

/*********************************************************************************
* @brief Inserts a new product record into the database.
* This method inserts a new product with the specified type into the database 
* and returns the newly generated ID.
* @param[in] conn: The SQL connection object.
* @param[in] xmlDoc: The XML document containing the product information.
* @return The ID of the newly inserted product.
*********************************************************************************/
    static int InsertNewProduct(SqlConnection conn, XDocument xmlDoc)
    {
        string productType = GetElementValue(xmlDoc, "ModelNumber");
        string sqlInsertProductData = "INSERT INTO dbo.TestProducts (Type) VALUES (@Type); SELECT CAST(SCOPE_IDENTITY() AS int);";

        using (SqlCommand cmdInsert = new SqlCommand(sqlInsertProductData, conn))
        {
            cmdInsert.Parameters.AddWithValue("@Type", productType);
            return (int)cmdInsert.ExecuteScalar();
        }
    }

/*********************************************************************************
* @brief Retrieves or creates a product family record in the database.
* This method checks if a product family with the specified name exists in the database.
* If it exists, its ID is returned. If not, a new product family is created, and its ID is returned.
* @param[in] conn: The SQL connection object.
* @param[in] xmlDoc: The XML document containing the product family information.
* @return The ID of the existing or newly created product family.
*********************************************************************************/
    static int GetorCreateProductFamily(SqlConnection conn, XDocument xmlDoc)
    {
        string productFamily = GetElementValue(xmlDoc, "ModelNumber");
        string prodFamily;

        if (productFamily.StartsWith("S"))
        {
            prodFamily = productFamily.Substring(5, 4);
        }
        else
        {
            prodFamily = productFamily.Substring(0, 3);
        }

        string sqlCheckFamily = "SELECT Family_ID FROM dbo.ProductFamilies WHERE ProductFamily = @ProductFamily";

        using (SqlCommand cmdCheck = new SqlCommand(sqlCheckFamily, conn))
        {
            cmdCheck.Parameters.AddWithValue("@ProductFamily", prodFamily);
            object existingId = cmdCheck.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                return (int)existingId;
            }
            else
            {
                return InsertNewProductFamily(conn, xmlDoc);
            }
        }
    }

/*********************************************************************************
* @brief Inserts a new product family record into the database.
* This method inserts a new product family with the specified name into the database 
* and returns the newly generated ID.
* @param[in] conn: The SQL connection object.
* @param[in] xmlDoc: The XML document containing the product family information.
* @return The ID of the newly inserted product family.
*********************************************************************************/
    static int InsertNewProductFamily(SqlConnection conn, XDocument xmlDoc)
    {
        string productFamily = GetElementValue(xmlDoc, "ModelNumber");

        if (productFamily.StartsWith("S"))
        {
            string prodFam = productFamily.Substring(5, 4);

            string sqlInsertFamilyData = "INSERT INTO dbo.ProductFamilies (ProductFamily) VALUES (@ProductFamily); SELECT CAST(SCOPE_IDENTITY() AS int);";

            using (SqlCommand cmdInsert = new SqlCommand(sqlInsertFamilyData, conn))
            {
                cmdInsert.Parameters.AddWithValue("@ProductFamily", prodFam);
                return (int)cmdInsert.ExecuteScalar();
            }
        }
        else
        {
            string prodFam = productFamily.Substring(0, 3);

            string sqlInsertFamilyData = "INSERT INTO dbo.ProductFamilies (ProductFamily) VALUES (@ProductFamily); SELECT CAST(SCOPE_IDENTITY() AS int);";

            using (SqlCommand cmdInsert = new SqlCommand(sqlInsertFamilyData, conn))
            {
                cmdInsert.Parameters.AddWithValue("@ProductFamily", prodFam);
                return (int)cmdInsert.ExecuteScalar();
            }
        }
    }

/*********************************************************************************
* @brief Retrieves or creates a customer record in the database.
* This method checks if a customer with the specified name exists in the database.
* If it exists, its ID is returned. If not, a new customer is created, and its ID is returned.
* @param[in] conn: The SQL connection object.
* @param[in] xmlDoc: The XML document containing the customer information.
* @return The ID of the existing or newly created customer.
*********************************************************************************/
    static int GetorCreateCustomer(SqlConnection conn, XDocument xmlDoc)
    {
        string productCustomer = GetElementValue(xmlDoc, "ModelNumber");
        string prodCustomer;
        if (productCustomer.StartsWith("S"))
        {
            prodCustomer = "SGRE";
        }
        else if (productCustomer.StartsWith("V"))
        {
            prodCustomer = "VESTAS";
        }
        else
        {
            prodCustomer = "Other";
        }

        string sqlCheckCustomer = "SELECT Customer_ID FROM dbo.Customer WHERE CustomerName = @CustomerName";

        using (SqlCommand cmdCheck = new SqlCommand(sqlCheckCustomer, conn))
        {
            cmdCheck.Parameters.AddWithValue("@CustomerName", prodCustomer);
            object existingId = cmdCheck.ExecuteScalar();

            if (existingId != null && existingId != DBNull.Value)
            {
                return (int)existingId;
            }
            else
            {
                return InsertNewCustomer(conn, xmlDoc);
            }
        }
    }

/*********************************************************************************
* @brief Inserts a new customer record into the database.
* This method inserts a new customer with the specified name into the database 
* and returns the newly generated ID.
* @param[in] conn: The SQL connection object.
* @param[in] xmlDoc: The XML document containing the customer information.
* @return The ID of the newly inserted customer.
*********************************************************************************/
    static int InsertNewCustomer(SqlConnection conn, XDocument xmlDoc)
    {
        string productCustomer = GetElementValue(xmlDoc, "ModelNumber");

        if (productCustomer.StartsWith("S"))
        {
            string prodCustomer = "SGRE";

            string sqlInsertCustomerData = "INSERT INTO dbo.Customer (CustomerName) VALUES (@CustomerName); SELECT CAST(SCOPE_IDENTITY() AS int);";
            using (SqlCommand cmdInsert = new SqlCommand(sqlInsertCustomerData, conn))
            {
                cmdInsert.Parameters.AddWithValue("@CustomerName", prodCustomer);
                return (int)cmdInsert.ExecuteScalar();
            }
        }
        else if (productCustomer.StartsWith("V"))
        {
            string prodCustomer = "VESTAS";

            string sqlInsertCustomerData = "INSERT INTO dbo.Customer (CustomerName) VALUES (@CustomerName); SELECT CAST(SCOPE_IDENTITY() AS int);";
            using (SqlCommand cmdInsert = new SqlCommand(sqlInsertCustomerData, conn))
            {
                cmdInsert.Parameters.AddWithValue("@CustomerName", prodCustomer);
                return (int)cmdInsert.ExecuteScalar();
            }
        }
        else
        {
            string prodCustomer = "Other";

            string sqlInsertCustomerData = "INSERT INTO dbo.Customer (CustomerName) VALUES (@CustomerName); SELECT CAST(SCOPE_IDENTITY() AS int);";
            using (SqlCommand cmdInsert = new SqlCommand(sqlInsertCustomerData, conn))
            {
                cmdInsert.Parameters.AddWithValue("@CustomerName", prodCustomer);
                return (int)cmdInsert.ExecuteScalar();
            }
        }
    }

    /*********************************************************************************
    * @brief Retrieves the ID of an existing test step from the database or creates a new one if it doesn't exist.
    * This method checks if a test step with the specified description already exists in the database.
    * If it does, it retrieves its ID. If not, it inserts a new test step with the provided description
    * into the database and returns its newly generated ID.
    * @param[in] conn: The SQL connection object.
    * @param[in] stepDescription: The description of the test step.
    * @return The ID of the existing or newly inserted test step.
    *********************************************************************************/
    static int GetOrCreateTestStep(SqlConnection conn, string stepDescription)
    {
        // SQL query to check if the step already exists in dbo.TestSteps
        string sqlCheckStep = "SELECT Step_ID FROM dbo.TestSteps WHERE StepDescription = @StepDescription";

        using (SqlCommand cmdCheck = new SqlCommand(sqlCheckStep, conn))
        {
            cmdCheck.Parameters.AddWithValue("@StepDescription", stepDescription);
            object existingStepId = cmdCheck.ExecuteScalar();

            if (existingStepId != null && existingStepId != DBNull.Value)
            {
                return (int)existingStepId;
            }
            else
            {
                return InsertNewTestStep(conn, stepDescription);
            }
        }
    }

    /*********************************************************************************
    * @brief Inserts a new test step into the database.
    * This method inserts a new test step with the provided description into the dbo.TestSteps table.
    * It then retrieves the newly generated ID of the inserted test step and returns it.
    * @param[in] conn: The SQL connection object.
    * @param[in] stepDescription: The description of the test step to be inserted.
    * @return The ID of the newly inserted test step.
    *********************************************************************************/
    static int InsertNewTestStep(SqlConnection conn, string stepDescription)
    {
        // SQL query to insert a new step into dbo.TestSteps
        string sqlInsertStep = "INSERT INTO dbo.TestSteps (StepDescription) VALUES (@StepDescription); SELECT CAST(SCOPE_IDENTITY() AS int);";

        using (SqlCommand cmdInsert = new SqlCommand(sqlInsertStep, conn))
        {
            cmdInsert.Parameters.AddWithValue("@StepDescription", stepDescription);
            return (int)cmdInsert.ExecuteScalar();
        }
    }

/*********************************************************************************
* @brief Inserts test data into the database.
* This method inserts a new test record into the database, including information about the product family, product, customer,
* test start and end dates, test times, and the test result.
* It returns the ID of the newly inserted test record.
* @param[in] conn: The SQL connection object.
* @param[in] familyId: The ID of the product family.
* @param[in] productId: The ID of the product.
* @param[in] customerId: The ID of the customer.
* @param[in] xmlDoc: The XML document containing test information.
* @param[in] startDate: The start date of the test.
* @param[in] startTime: The start time of the test.
* @param[in] endDate: The end date of the test.
* @param[in] endTime: The end time of the test.
* @param[in] result: The result of the test.
* @return The ID of the newly inserted test record.
*********************************************************************************/
    static int InsertTestData(SqlConnection conn, int existingFamilyId, int existingProductId, int existingCustomerId, XDocument xmlDoc, string testStartDate, string testStartTime, string testEndDate, string testEndTime, string Result)
    {
        string serialNumber = GetElementValue(xmlDoc, "SerialNumber");
        string stationId = GetElementValue(xmlDoc, "StationID");
        string testSequenceVersion = GetElementValue(xmlDoc, "Test Sequence Version");
        string operatorName = GetElementValue(xmlDoc, "LoginName");

        string sqlInsertTestData = @"
        INSERT INTO dbo.TestData (Product_ID, Family_ID, Customer_ID, SerialNumber, TestStartDate, TestStartTime, TestEndDate, TestEndTime, Station_ID, TestSequenceVersion, Operator, Result)
        VALUES (@Product_ID, @Family_ID, @Customer_ID, @SerialNumber, @TestStartDate, @TestStartTime, @TestEndDate, @TestEndTime, @Station_ID, @TestSequenceVersion, @Operator, @Result);
        SELECT CAST(SCOPE_IDENTITY() AS int);";

        using (SqlCommand cmd = new SqlCommand(sqlInsertTestData, conn))
        {
            cmd.Parameters.AddWithValue("@Product_ID", existingProductId);
            cmd.Parameters.AddWithValue("@Family_ID", existingFamilyId);
            cmd.Parameters.AddWithValue("@Customer_ID", existingCustomerId);
            cmd.Parameters.AddWithValue("@SerialNumber", serialNumber);
            cmd.Parameters.AddWithValue("@TestStartDate", testStartDate);
            cmd.Parameters.AddWithValue("@TestStartTime", testStartTime);
            cmd.Parameters.AddWithValue("@TestEndDate", testEndDate);
            cmd.Parameters.AddWithValue("@TestEndTime", testEndTime);
            cmd.Parameters.AddWithValue("@Station_ID", stationId);
            cmd.Parameters.AddWithValue("@TestSequenceVersion", testSequenceVersion);
            cmd.Parameters.AddWithValue("@Operator", operatorName);
            cmd.Parameters.AddWithValue("@Result", Result);

            try
            {
                int testId = (int)cmd.ExecuteScalar();
                if (testId <= 0)
                {
                    Console.WriteLine("Error inserting test data.");
                    return -1;
                }

                Console.WriteLine("Test ID: " + testId);
                return testId;
            }
            catch (SqlException ex)
            {
                Console.WriteLine("Error executing SQL statement: " + ex.Message);
                return -1;
            }
        }
    }

/*********************************************************************************
* @brief Inserts test step data into the database.
* This method inserts multiple records into the test steps table, associating each test step with its corresponding test and step IDs.
* @param[in] conn: The SQL connection object.
* @param[in] testId: The ID of the test to which the steps belong.
* @param[in] stepId: The ID of the test step.
* @param[in] stepsData: A collection of dynamic objects representing the step data to be inserted.
* @return None.
*********************************************************************************/
    static void InsertTestStepsData(SqlConnection conn, int testId, int stepId, IEnumerable<dynamic> callTestPrimitiveElements)
    {
        string sqlInsertStepData = @"
        INSERT INTO dbo.TestStepData (Test_ID, Step_ID, StepStatus, StepTime, Measurement, LowTolerance, HighTolerance, NumLoops, NumPassed, NumFailed)
        VALUES (@Test_ID, @Step_ID, @StepStatus, @StepTime, @Measurement, @LowTolerance, @HighTolerance, @NumLoops, @NumPassed, @NumFailed);";

        using (SqlCommand cmd = new SqlCommand(sqlInsertStepData, conn))
        {
            foreach (var element in callTestPrimitiveElements)
            {
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@Test_ID", testId);
                cmd.Parameters.AddWithValue("@Step_ID", stepId);
                cmd.Parameters.AddWithValue("@StepStatus", (object)element.Status ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@StepTime", (object)element.TotalTime ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Measurement", (object)element.Numeric ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@LowTolerance", (object)element.Low ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@HighTolerance", (object)element.High ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NumLoops", (object)element.NumLoops ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NumPassed", (object)element.NumPassed ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@NumFailed", (object)element.NumFailed ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }
    }
}