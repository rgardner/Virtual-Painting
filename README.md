# Virtual Painting

Be who you want to be! Virtual Painting is a fun photo booth experience
where you can take a selfie and then construct a new identity.

This project is intended to be deployed in an exhibition environment, so it
automatically resets back to the photo booth screen after 10 seconds of
inactivity.

## Installation

```sh
git clone https://github.com/rgardner/Virtual-Painting.git
```

[Install the Kinect for Windows SDK 2.0](https://www.microsoft.com/en-us/download/details.aspx?id=44561)

## Usage

1. Open the solution in Visual Studio
2. Click Start

## Configuration

The directories where the final images are saved and where the selfies are
saved can be configured by environment variables:

```text
VirtualPainting_SavedImagesDirectoryPath=C:\Users\<User>\OneDrive
VirtualPainting_SavedBackgroundImagesDirectoryPath=C:\temp
```

## Credit

This was forked from [Kinect Drawing](https://github.com/LightBuzz/Kinect-Drawing),
created by Vangos Pterneas. His blog posts were also very useful during
development of this project, particularly
[Understanding Kinect Coordinate Mapping](https://pterneas.com/2014/05/06/understanding-kinect-coordinate-mapping/).

## License

MIT
