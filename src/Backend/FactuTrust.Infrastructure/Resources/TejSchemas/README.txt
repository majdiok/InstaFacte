TEJ XML schemas (XSD)
=====================

Place official XSD files from the Tunisian tax administration / TEJ portal here and mark them as EmbeddedResource in FactuTrust.Infrastructure.csproj.

FactuTrust ships TejDeclarationsRS.envelope.xsd as a structural envelope (root DeclarationsRS, Declarant, ReferenceDeclaration, certificate blocks). Inner certificate markup is not constrained by this file so that exports remain usable until official schemas are added.

When you add official schemas, remove or narrow the xs:any / processContents="skip" sections and run the test suite (TejXmlValidatorTests, TejXmlGeneratorGoldenTests).

Compare each element name and date/amount formats with the latest TEJ portal guide (DeclarationsRS, Certificat, Operation, millimes as integers, dates JJ/MM/AAAA).
