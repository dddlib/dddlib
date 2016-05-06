CREATE TABLE [dbo].[Cars]
(
    [Registration] VARCHAR(10) NOT NULL PRIMARY KEY,
    [TotalDistanceDriven] INT NOT NULL
);
GO

CREATE PROCEDURE [dbo].[LoadCar]
    @Registration VARCHAR(10)
AS

SELECT [Registration], [TotalDistanceDriven]
FROM [dbo].[Cars]
WHERE [Registration] = @Registration;

GO

CREATE PROCEDURE [dbo].[SaveCar]
    @Registration VARCHAR(10),
    @TotalDistanceDriven INT,
    @IsDestroyed BIT
AS

MERGE INTO [dbo].[Cars] AS [Target]
USING (SELECT @Registration AS [Registration], @TotalDistanceDriven AS [TotalDistanceDriven], @IsDestroyed AS [IsDestroyed]) AS [Source]
    ON [Target].[Registration] = [Source].[Registration]
WHEN MATCHED AND [Source].[IsDestroyed] = 1 THEN
    DELETE
WHEN MATCHED THEN
    UPDATE SET [Target].[TotalDistanceDriven] = [Source].[TotalDistanceDriven]
WHEN NOT MATCHED THEN
    INSERT ([Registration], [TotalDistanceDriven])
    VALUES ([Source].[Registration], [Source].[TotalDistanceDriven]);

GO