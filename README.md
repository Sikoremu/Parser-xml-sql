**Important:**
The project was implemented using tools such as SQL Server, Power BI and artificial intelligence support.
AI helped in generating some complicated SQL queries, Parser-xml-sql source code and optimizing analytical processes, but key decisions and visualizations were created in accordance with my concept and project requirements.
Thanks to this, the project meets the assumed goals and delivers valuable business conclusions.

**Changes:**
1. Due to usage of one data source (SQL Server) by Gambit and ATE/SATE Dashboard, I decided to merge them.
2. Instead of using a BIs KPI visualisation I decided to create trend arrow which shows us if KPI from previous data range is higher or lower:
  **- Measure which is calculating previous start data range:**
      PreviousDateRangeStart = 
      VAR MinDate = MIN(DateTable[Date])
      VAR MaxDate = MAX(DateTable[Date])
      VAR IsSingleDaySelection = MinDate = MaxDate
      VAR IsWeekSelection = WEEKNUM(MinDate, 2) = WEEKNUM(MaxDate, 2)
      VAR IsMonthSelection = MONTH(MinDate) = MONTH(MaxDate)
      VAR IsYearSelection = YEAR(MinDate) = YEAR(MaxDate)

      RETURN
        SWITCH(
          TRUE(),
          IsSingleDaySelection, MinDate - 1,
          IsWeekSelection, MinDate - 7,
          IsMonthSelection, EDATE(MinDate, -1),
          IsYearSelection, DATE(YEAR(MinDate) - 1, MONTH(MinDate), DAY(MinDate)),
          BLANK()
        )

  **- Measure which is calculating previous end data range:**
      PreviousDateRangeEnd = 
      VAR MinDate = MIN(DateTable[Date])
      VAR MaxDate = MAX(DateTable[Date])
      VAR IsSingleDaySelection = MinDate = MaxDate
      VAR IsWeekSelection = WEEKNUM(MinDate, 2) = WEEKNUM(MaxDate, 2)
      VAR IsMonthSelection = MONTH(MinDate) = MONTH(MaxDate)
      VAR IsYearSelection = YEAR(MinDate) = YEAR(MaxDate)
      RETURN
        SWITCH(
          TRUE(),
          IsSingleDaySelection, MaxDate - 1,
          IsWeekSelection, MaxDate - 7,
          IsMonthSelection, EOMONTH(MaxDate, -1),
          IsYearSelection, DATE(YEAR(MaxDate) - 1, MONTH(MaxDate), DAY(MaxDate)),
          BLANK()
      )

  **- Measure which is calculating previous data range apparent KPI:**
      PreviousApparentFRY = 
      CALCULATE(
        AVERAGE(vw_ApparentFRYTestPreviousResults[FRY]),
        FILTER(
          vw_ApparentFRYTestPreviousResults,
          vw_ApparentFRYTestPreviousResults[TestEndDate] >= [PreviousDateRangeStart] &&
          vw_ApparentFRYTestPreviousResults[TestEndDate] <= [PreviousDateRangeEnd]
        )
      )

  **- Measure which is calculating difference between KPIs:**
      ApparentFRYDifference = 
        CALCULATE(
          AVERAGE(vw_ApparentFRYTestResults[FRY])
        ) - [PreviousApparentFRY]

  **- Measure which is showing KPIs trend indicator:**
      TrendIndicator = 
      VAR Difference = [ApparentFRYDifference]
      RETURN
        SWITCH(
            TRUE(),
            Difference > 0, UNICHAR(9650),  -- Zielona strzałka w górę
            Difference < 0, UNICHAR(9660),-- Czerwona strzałka w dół
            UNICHAR(9654)  -- Neutralna strzałka
              )
