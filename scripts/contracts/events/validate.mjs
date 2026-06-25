import fs from "node:fs";
import path from "node:path";
import process from "node:process";
import Ajv2020 from "ajv/dist/2020.js";
import addFormats from "ajv-formats";

const rootDir = process.cwd();
const contractsDir = path.join(rootDir, "contracts", "events");
const examplesDir = path.join(contractsDir, "examples");

const ajv = new Ajv2020({
  allErrors: true,
  strict: false,
  validateSchema: true
});

addFormats(ajv);

const failures = [];

function relative(filePath) {
  return path.relative(rootDir, filePath).replaceAll(path.sep, "/");
}

function readJson(filePath) {
  try {
    return JSON.parse(fs.readFileSync(filePath, "utf8"));
  } catch (error) {
    failures.push(`${relative(filePath)}: JSON invalido. ${error.message}`);
    return undefined;
  }
}

function listFiles(directory, predicate) {
  if (!fs.existsSync(directory)) {
    failures.push(`${relative(directory)}: diretorio nao encontrado.`);
    return [];
  }

  return fs
    .readdirSync(directory, { withFileTypes: true })
    .filter((entry) => entry.isFile())
    .map((entry) => path.join(directory, entry.name))
    .filter(predicate)
    .sort((left, right) => relative(left).localeCompare(relative(right)));
}

function formatErrors(errors) {
  return (errors ?? [])
    .map((error) => {
      const instancePath = error.instancePath || "/";
      return `${instancePath} ${error.message}`;
    })
    .join("; ");
}

const schemaFiles = listFiles(contractsDir, (filePath) =>
  filePath.endsWith(".schema.json")
);

const schemas = new Map();

for (const schemaFile of schemaFiles) {
  const schema = readJson(schemaFile);
  if (schema === undefined) {
    continue;
  }

  try {
    const validate = ajv.compile(schema);
    const contractName = path.basename(schemaFile, ".schema.json");
    schemas.set(contractName, {
      file: schemaFile,
      validate
    });
  } catch (error) {
    failures.push(`${relative(schemaFile)}: schema invalido. ${error.message}`);
  }
}

if (schemas.size === 0) {
  failures.push("contracts/events: nenhum arquivo .schema.json encontrado.");
}

const exampleFiles = listFiles(examplesDir, (filePath) =>
  filePath.endsWith(".valid.json") || filePath.endsWith(".invalid.json")
);

const validExamplesBySchema = new Map();

for (const exampleFile of exampleFiles) {
  const fileName = path.basename(exampleFile);
  const isValidExample = fileName.endsWith(".valid.json");
  const suffix = isValidExample ? ".valid.json" : ".invalid.json";
  const contractName = fileName.slice(0, -suffix.length);
  const schemaEntry = schemas.get(contractName);

  if (!schemaEntry) {
    failures.push(
      `${relative(exampleFile)}: exemplo sem schema correspondente ${contractName}.schema.json.`
    );
    continue;
  }

  const example = readJson(exampleFile);
  if (example === undefined) {
    continue;
  }

  const passed = schemaEntry.validate(example);

  if (isValidExample) {
    validExamplesBySchema.set(contractName, true);

    if (!passed) {
      failures.push(
        `${relative(exampleFile)}: exemplo valido falhou contra ${relative(
          schemaEntry.file
        )}. ${formatErrors(schemaEntry.validate.errors)}`
      );
    }
  } else if (passed) {
    failures.push(
      `${relative(exampleFile)}: exemplo invalido passou contra ${relative(
        schemaEntry.file
      )}. Ajuste o exemplo para cobrir uma violacao real do contrato.`
    );
  }
}

for (const [contractName, schemaEntry] of schemas) {
  if (!validExamplesBySchema.has(contractName)) {
    failures.push(
      `${relative(schemaEntry.file)}: nenhum exemplo ${contractName}.valid.json encontrado.`
    );
  }
}

if (failures.length > 0) {
  console.error("Validacao de contratos de eventos falhou:");
  for (const failure of failures) {
    console.error(`- ${failure}`);
  }

  process.exit(1);
}

console.log(
  `Contratos de eventos validados com sucesso: ${schemas.size} schema(s), ${exampleFiles.length} exemplo(s).`
);
