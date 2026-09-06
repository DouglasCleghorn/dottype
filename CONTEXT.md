# dottype

dottype describes data as C# types inferred from JSON, CSV, or Parquet input.

## Language

**dottype**:
Always spell the name in lowercase, including UI labels, configuration, and documentation.

**Example file**:
A data file used as the source for inferring the types an application will use. It can be distinct from the data the application processes when it runs.
_Avoid_: Template file

**Inference sample**:
The initial complete records from an example file that dottype examines to infer its schema. Fields and values found only outside this sample do not contribute to that schema.
_Avoid_: Hash sample

**Inferred schema**:
The description of data fields, their types, and their nullability derived from the input examined by dottype.

**Generated type**:
A C# data type produced from an inferred schema, preserving source field names directly or through mapping metadata.
_Avoid_: Runtime type
