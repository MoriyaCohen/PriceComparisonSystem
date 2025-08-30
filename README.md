# 📊 PriceComparisonSystem

A telephone & web-based price comparison system, leveraging government price transparency data.  
Developed by two Full Stack developers as an independent project, designed to meet enterprise standards.

---

## 🚀 Technologies
- **Frontend:** Angular 19 Standalone  
- **Backend:** .NET 8 Web API + Clean Architecture  
- **Database:** SQL Server  
- **Data Processing:** XML Parsing, Automated Download Jobs  
- **Tools:** Swagger, Postman, GitHub Actions, Serilog, AutoMapper  

---

## 📂 Project Structure
- `PriceComparison.Api` – Main API (Controllers, Services)  
- `PriceComparison.Application` – Business logic (Use Cases, DTOs)  
- `PriceComparison.Infrastructure` – Database, Repositories  
- `PriceComparison.Download` – Automated XML file downloads from retail chains  
- `price-comparison-frontend` – Angular project (user interface)  
- `docs` – Documentation (specs, ERD, UML, prototype docs)  

---

## ✨ Features
- 📥 Automated daily download of XML files from all retail chains  
- 🔍 Search by barcode or category  
- 💰 Price comparison – cheapest, average, most expensive  
- 🗄️ MemoryCache for dynamic data + SQL Server for static data  
- 📞 Telephone IVR interface support for basic phones  

---

## 📌 Status
- ✅ Prototype 1 completed – comparison from a single XML file  
- ✅ Barcode search implemented  
- 🚧 In progress – migration to cloud server deployment  

---

## 👩‍💻 Team
- **Moriya Cohen** – Full Stack Developer (Angular, .NET, SQL)  
- **Shoshi Hershler** – Full Stack Developer (Angular, C#, Azure)  

---

## ⚙️ Installation
```bash
# Clone repo
git clone https://github.com/MoriyaCohen/PriceComparisonSystem.git

# Backend
cd PriceComparison.Api
dotnet run

# Frontend
cd price-comparison-frontend
npm install
ng serve
