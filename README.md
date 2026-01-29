# CSharp-SQLServer-DAL-BLL-Code-Generator

A Windows Forms tool that automatically generates **Data Access Layer (DAL)** and **Business Logic Layer (BLL)** code from SQL Server databases.

This project was built to eliminate repetitive boilerplate code and speed up backend development in real-world projects.

---

## 🚀 Features

- Generate DAL & BLL layers automatically
- Full CRUD operations (Add, Update, Delete, Find, GetAll)
- Correct handling of nullable and non-nullable columns
- Clean and scalable architecture
- Strong separation between Data Access and Business Logic
- Easy to extend and customize for any project
- Works with real SQL Server databases

---

## 🧠 Why This Project?

In large systems, most tables share the same CRUD patterns.
Writing the same DAL and BLL code repeatedly wastes time and increases the chance of errors.

This tool was created to:
- Save development time
- Improve consistency across projects
- Focus on business logic instead of repetitive code

---

## ⚠️ Notes About Database Design

- Tables with a **single primary key** generate full CRUD operations.
- Tables with **composite primary keys** generate **GetAll** operations only.

This design choice keeps the generated code simple, safe, and aligned with common real-world use cases.

---

## 🛠 Technologies Used

- C#
- .NET Framework
- Windows Forms
- SQL Server
- ADO.NET

---

## 📦 Download

You can download the compiled executable here:  
👉 **[Download EXE](https://github.com/Alkumait10/CSharp-SQLServer-DAL-BLL-Code-Generator/releases/download/v1.0.0/CodeGenerator_Setup.exe)**

---

## 📌 Future Improvements

- Custom handling for composite primary keys
- Configurable generation options
- Support for additional database engines

---

## 🤝 Contributions

Feel free to open issues, suggest improvements, or submit pull requests.

---

## 📧 Contact

If you have feedback or collaboration ideas, feel free to reach out.

## 👨‍💻 Author
Alkumait Ghanem
