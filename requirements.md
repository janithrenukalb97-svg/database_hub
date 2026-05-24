# DB Release Manager - Requirements

## Overview
A WPF C# application for managing database releases, script execution, and version control integration with Azure TFS.

## Core Application Requirements

### 1. Database Connection Management
- **Authentication**: Implement Windows AD authentication for connecting to company databases (SQL Server assumed based on context)
- **Connection Handling**: Support multiple database connections with connection string management
- **Security**: Store connection credentials securely (Windows Credential Manager or encrypted config)
- **Connection Testing**: Provide UI to test and validate database connections before use

### 2. Version Control Integration (Azure TFS)
- **Repository Connection**: Connect to Azure TFS repository for database scripts
- **Script Upload**: Push database script changes to repository through the application
- **Version Management**: Track release versions and associate scripts with specific versions
- **Team Member Tracking**: Link script changes to team members who made them
- **Change History**: View history of script changes and versions

### 3. File System Integration
- **Folder Browser**: Open Windows folder picker to select script directories
- **SQL Object Detection**: Scan folders and identify SQL-related files (stored procedures, functions, views, tables, etc.)
- **Folder Structure Display**: Show hierarchical view of SQL objects matching folder structure
- **Script Execution**: Execute selected SQL scripts against connected databases
- **Execution Logging**: Track script execution results, success/failure, and timestamps

### 4. Script Comparison Tool
- **Dual Source Support**: Compare scripts from:
  - File system (selected files)
  - Database objects (retrieved from connected databases)
- **Side-by-Side View**: Display two scripts in parallel panels
- **Diff Highlighting**: Show differences between scripts with color coding
- **Navigation**: Jump between differences, merge changes
- **Export Results**: Save comparison results to file

### 5. Database Object Search and Preview
- **Object Type Selection**: Dropdown/filter for SQL object types (tables, views, procedures, functions, triggers, etc.)
- **Name Search**: Partial name matching for database objects
- **Script Retrieval**: Generate CREATE scripts for selected database objects
- **Preview Display**: Syntax-highlighted script preview in the application
- **Export Options**: Save retrieved scripts to files

## Technical Architecture Considerations

### UI Framework
- WPF with MVVM pattern
- Modern UI controls (possibly Material Design or custom styling)
- Responsive layout for different screen sizes

### Data Layer
- ADO.NET or Entity Framework for database operations
- Support for SQL Server (System.Data.SqlClient)
- Asynchronous operations for UI responsiveness

### Version Control
- Azure DevOps REST API or TFS SDK for repository operations
- Git operations for pushing changes

### File Operations
- System.IO for file system access
- SQL parsing libraries for script analysis
- Background workers for large directory scans

### Additional Features
- Error handling and user feedback
- Progress indicators for long-running operations
- Logging system for audit trails
- Configuration management
- Help/documentation integration

## Development Phases

1. **Phase 1**: Core UI shell and database connection management
2. **Phase 2**: File system integration and script execution
3. **Phase 3**: Version control integration
4. **Phase 4**: Script comparison and object search features
5. **Phase 5**: Testing, refinement, and deployment

## Technologies
- **Language**: C# .NET 6.0+
- **UI Framework**: WPF
- **Database**: SQL Server
- **Version Control**: Azure TFS/Azure DevOps
- **Additional Libraries**:
  - Microsoft.Data.SqlClient
  - Microsoft.TeamFoundationServer.Client
  - DiffPlex (for script comparison)
  - AvalonEdit or ICSharpCode.TextEditor (for syntax highlighting)