**Important:**
The project was implemented using tools such as SQL Server, Power BI and artificial intelligence support.
AI helped in generating some complicated SQL queries, Parser-xml-sql source code and optimizing analytical processes, but key decisions and visualizations were created in accordance with my concept and project requirements.
Thanks to this, the project meets the assumed goals and delivers valuable business conclusions.

**Changes:**
1. Due to usage of one data source (SQL Server) by Gambit and ATE/SATE Dashboard, I decided to merge them.
2. Instead of using a BIs KPI visualisation I decided to create trend arrow which shows us if KPI from previous data range is higher or lower:
  - **- Measure which is calculating previous start data range: PreviousDateRangeStart**
  - **- Measure which is calculating previous end data range: PreviousDateRangeEnd**
  - **- Measure which is calculating previous data range apparent KPI: PreviousApparentFRY**
  - **- Measure which is calculating difference between KPIs: ApparentFRYDifference**
  - **- Measure which is showing KPIs trend indicator: TrendIndicator**
