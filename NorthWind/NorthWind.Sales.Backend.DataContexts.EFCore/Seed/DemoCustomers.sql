USE [NorthWindDB];
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Customers TABLE
(
    Id nchar(5) NOT NULL PRIMARY KEY,
    Identification nvarchar(30) NOT NULL UNIQUE,
    Name nvarchar(100) NOT NULL,
    Address nvarchar(250) NOT NULL,
    Email nvarchar(254) NOT NULL UNIQUE,
    Phone nvarchar(25) NOT NULL,
    City nvarchar(15) NOT NULL,
    Country nvarchar(15) NOT NULL,
    PostalCode nvarchar(10) NOT NULL
);

INSERT INTO @Customers (Id, Identification, Name, Address, Email, Phone, City, Country, PostalCode) VALUES
(N'ALFKI',N'0910000017',N'Comercial Alfa',N'Av. Amazonas 100 y Naciones Unidas',N'demo.alfki@example.test',N'0990000001',N'Quito',N'Ecuador',N'170101'),
(N'ANATR',N'0910000025',N'Distribuidora Anatra',N'Av. 9 de Octubre 200 y Chile',N'demo.anatr@example.test',N'0990000002',N'Guayaquil',N'Ecuador',N'090101'),
(N'BONAP',N'0910000033',N'Comercial Bonaparte',N'Av. Solano 300 y Remigio Crespo',N'demo.bonap@example.test',N'0990000003',N'Cuenca',N'Ecuador',N'010101'),
(N'D0001',N'0910000041',N'María Fernanda López',N'Calle Loja 101',N'demo.cliente01@example.test',N'0990000004',N'Loja',N'Ecuador',N'110101'),
(N'D0002',N'0910000058',N'Carlos Andrés Pérez',N'Av. Malecón 202',N'demo.cliente02@example.test',N'0990000005',N'Manta',N'Ecuador',N'130101'),
(N'D0003',N'0910000066',N'Daniela Alejandra Torres',N'Calle Bolívar 303',N'demo.cliente03@example.test',N'0990000006',N'Ambato',N'Ecuador',N'180101'),
(N'D0004',N'0910000074',N'Juan Sebastián Morales',N'Av. Mariano Acosta 404',N'demo.cliente04@example.test',N'0990000007',N'Ibarra',N'Ecuador',N'100101'),
(N'D0005',N'0910000082',N'Andrea Carolina Sánchez',N'Calle Primera Constituyente 505',N'demo.cliente05@example.test',N'0990000008',N'Riobamba',N'Ecuador',N'060101'),
(N'D0006',N'0910000090',N'Luis Fernando Herrera',N'Av. Amazonas 606',N'demo.cliente06@example.test',N'0990000009',N'Latacunga',N'Ecuador',N'050101'),
(N'D0007',N'0910000108',N'Distribuidora Los Andes',N'Calle Malecón 707',N'demo.cliente07@example.test',N'0990000010',N'Salinas',N'Ecuador',N'240101'),
(N'D0008',N'0910000116',N'Comercial Ambato',N'Av. Manabí 808',N'demo.cliente08@example.test',N'0990000011',N'Portoviejo',N'Ecuador',N'130102'),
(N'D0009',N'0910000124',N'Ferretería El Constructor',N'Calle Rocafuerte 909',N'demo.cliente09@example.test',N'0990000012',N'Machala',N'Ecuador',N'070101'),
(N'D0010',N'0910000132',N'Papelería Central',N'Av. Libertad 1010',N'demo.cliente10@example.test',N'0990000013',N'Esmeraldas',N'Ecuador',N'080101'),
(N'D0011',N'0910000140',N'Tecnología y Soluciones',N'Calle Sucre 1111',N'demo.cliente11@example.test',N'0990000014',N'Tulcán',N'Ecuador',N'040101'),
(N'D0012',N'0910000157',N'Almacenes La Colina',N'Av. 10 de Agosto 1212',N'demo.cliente12@example.test',N'0990000015',N'Babahoyo',N'Ecuador',N'120101'),
(N'D0013',N'0910000165',N'Importadora Tungurahua',N'Calle Atahualpa 1313',N'demo.cliente13@example.test',N'0990000016',N'Puyo',N'Ecuador',N'160101'),
(N'D0014',N'0910000173',N'Comercial San Pedro',N'Av. 15 de Noviembre 1414',N'demo.cliente14@example.test',N'0990000017',N'Tena',N'Ecuador',N'150101'),
(N'D0015',N'0910000181',N'Servicios Integrales del Ecuador',N'Calle Amazonas 1515',N'demo.cliente15@example.test',N'0990000018',N'Zamora',N'Ecuador',N'190101'),
(N'D0016',N'0910000199',N'Valeria Fernanda Cedeño',N'Av. de la Ciudad 1616',N'demo.cliente16@example.test',N'0990000019',N'Macas',N'Ecuador',N'140101'),
(N'D0017',N'0910000207',N'José Antonio Zambrano',N'Calle Matovelle 1717',N'demo.cliente17@example.test',N'0990000020',N'Azogues',N'Ecuador',N'030101');

IF EXISTS
(
    SELECT 1 FROM @Customers c
    CROSS APPLY
    (
        SELECT SUM(x.DigitValue) AS CheckSumValue
        FROM (VALUES (1),(2),(3),(4),(5),(6),(7),(8),(9)) n(Position)
        CROSS APPLY (SELECT CONVERT(int, SUBSTRING(c.Identification, n.Position, 1)) AS Value) v
        CROSS APPLY (SELECT CASE WHEN n.Position % 2 = 1
            THEN CASE WHEN v.Value * 2 > 9 THEN v.Value * 2 - 9 ELSE v.Value * 2 END
            ELSE v.Value END AS DigitValue) x
    ) d
    WHERE LEN(c.Identification) <> 10
       OR c.Identification COLLATE Latin1_General_100_BIN2 LIKE N'%[^0-9]%'
       OR TRY_CONVERT(int, LEFT(c.Identification, 2)) NOT BETWEEN 1 AND 24
       OR SUBSTRING(c.Identification, 3, 1) NOT LIKE N'[0-5]'
       OR (10 - d.CheckSumValue % 10) % 10 <> CONVERT(int, RIGHT(c.Identification, 1))
)
    THROW 51000, 'La carga contiene una cédula ecuatoriana inválida.', 1;

IF EXISTS
(
    SELECT 1 FROM @Customers c
    INNER JOIN dbo.Customers existing ON existing.Identification = c.Identification
    WHERE existing.Id <> c.Id
)
    THROW 51001, 'Una cédula de demostración ya pertenece a otro cliente.', 1;

IF EXISTS
(
    SELECT 1 FROM @Customers c
    INNER JOIN dbo.Customers existing ON existing.Email = c.Email
    WHERE existing.Id <> c.Id
)
    THROW 51002, 'Un correo de demostración ya pertenece a otro cliente.', 1;

BEGIN TRANSACTION;

UPDATE customer
SET Name = seed.Name,
    Identification = seed.Identification,
    Address = seed.Address,
    Email = seed.Email,
    Phone = seed.Phone,
    City = seed.City,
    Country = seed.Country,
    PostalCode = seed.PostalCode,
    CurrentBalance = 0
FROM dbo.Customers customer
INNER JOIN @Customers seed ON seed.Id = customer.Id
WHERE seed.Id IN (N'ALFKI',N'ANATR',N'BONAP');

UPDATE customer
SET Name = seed.Name
FROM dbo.Customers customer
INNER JOIN @Customers seed ON seed.Id = customer.Id
WHERE customer.Name LIKE N'Cliente Demo [0-9][0-9]'
   OR customer.Name LIKE N'Cliente Prueba [0-9][0-9]'
   OR customer.Name LIKE N'% (ficticio)';

INSERT INTO dbo.Customers (Id, CurrentBalance, Name, Address, Email, Identification, Phone, City, Country, PostalCode)
SELECT seed.Id, 0, seed.Name, seed.Address, seed.Email, seed.Identification, seed.Phone, seed.City, seed.Country, seed.PostalCode
FROM @Customers seed
WHERE seed.Id NOT IN (N'ALFKI',N'ANATR',N'BONAP')
  AND NOT EXISTS (SELECT 1 FROM dbo.Customers existing WHERE existing.Id = seed.Id);

COMMIT TRANSACTION;

SELECT COUNT(*) AS TotalCustomers,
       SUM(CASE WHEN Identification IS NOT NULL AND LEN(Identification) = 10 THEN 1 ELSE 0 END) AS CustomersWithTenDigitIdentification,
       SUM(CASE WHEN CurrentBalance = 0 THEN 1 ELSE 0 END) AS ZeroBalanceCustomers
FROM dbo.Customers;
