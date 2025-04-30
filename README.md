# BHoM_JSONSchema
Welcome to the BHoM_JSONSchema repository that hosts [Json-Schemas](https://json-schema.org/) for a majority of the objects defined in the object models across the [BHoM organisation](https://github.com/BHoM).

The Json schemas makes in possible to validate your serialised json objects to ensure they are compatible and able to be deserialised into the BHoM ecosystem. 

If you like to get your BHoM oM included, please [raise an Issue here](https://github.com/BHoM/BHoM_JSONSchema/issues/new/choose).


## Validation
The schemas can be validated using various existing JSON schema validators. This has so far been tested and ensured to be functioning with the following validators.

### https://www.jsonschemavalidator.net/

To use this tool, simply put the Id of the schema you want to evalusate against as a `$ref` in the left hand pane, and the Json of the object you want to validate in the right hand pane. For example, to evaluate against a [Line](https://github.com/BHoM/BHoM_JSONSchema/blob/develop/Geometry_oM/Line.json), put the following in the left hand side pane:

```JSON
{
 "$ref":"https://raw.githubusercontent.com/BHoM/BHoM_JSONSchema/develop/Geometry_oM/Line.json"
}
```

To find the Id of the object you want to validate, please locate the schema in the folders of this repo and look at the `$id` property. Alternatively, you can navigate to the file in the browser and hit the "raw" button, and copy the link as the `$ref`.

### [JsonEverything](https://docs.json-everything.net/schema/basics/#schema-evaluation-2)

Has been tested and working for validation within the C# environment, building your custom evaluator.

## User notice
This repository is in early stage of development and therby prone to potential change, as well as not as deeply validated through the test of time. If you have any issues or ideas for improvement, please do [raise an Issue here](https://github.com/BHoM/BHoM_JSONSchema/issues/new/choose)
