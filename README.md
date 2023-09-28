[![Deploy](https://get.pulumi.com/new/button.svg)](https://app.pulumi.com/new?template=https://github.com/pulumi/examples/blob/master/aws-cs-fargate/README.md)

# Dockerized ASP.NET App on AWS ECS Fargate

This example defines a basic ASP.NET application and all of the infrastructure required to run it in AWS in C#.

This infrastructure includes everything needed to:

* Build and publish the ASP.NET applications (UI and API) as a Docker container images
* Ready to scale out 3 load-balanced replicas using [Amazon Elastic Container Service (ECS)](https://aws.amazon.com/ecs/) "Fargate" (eliminated for simplicity)
* Accept Internet traffic on port 80 using [Amazon Elastic Application Load Balancer (ELB)](https://aws.amazon.com/elasticloadbalancing/) for UI where API is running on private VPC and not accessible from the public

This example is inspired by [Docker's](https://docs.docker.com/get-started/) and
[ASP.NET's](https://docs.microsoft.com/en-us/aspnet/core/getting-started/?view=aspnetcore-3.1) Getting Started
tutorials. The result is a simple development experience and yet an end result that uses modern, production-ready AWS infrastructure. [`./devops/Program.cs`](./devops/Program.cs) defines the project's infrastructure.

## Technical assessment

Includes the attached project folder with docker files and a docker-compose file. 

The application is composed of 

* A Web UI
* A Web API

The web UI makes a http request to Web API And displays the response on the UI.

You can run the application on your local machine by executing:

`docker-compose up —build`

Note: make sure to have docker installed on your machine.

Requirements:

* This infrastructure is a production quality infrastructure.
* The coding exercise is to run this environment in AWS.
* We are looking for an infrastructure-as-code (IaC) solution using Pulumi.
* You are free to architect the infrastructure as you see fit. The final goal is to run both services (Web and API) on AWS. We are leaving all the details to you.
* Be mindful of security best practices. The API is not a public API, and it is only accessed by Web UI.
* Only the web UI should be accessible through the internet (port 80)
* Tag all the environment resources for auditing purposes.
 

Submission:

* Use a git source control for your submission (GitHub, GitLab, ...)
* A Diagram of infrastructure components along with your IaC.


## Simplified architecture diagram

![Simplified architecture diagram](Test%20diagram.png)

> There are 2 load balancers for scalability:
> * Public facing that accepts internet traffic
> * Internal that accepts traffic from UI to API

## Prerequisites

* [Install Pulumi](https://www.pulumi.com/docs/get-started/install/)
* [Configure Pulumi to Use AWS](https://www.pulumi.com/docs/intro/cloud-providers/aws/setup/) (if your AWS CLI is configured, no further changes are required)
* [Install .NET 6](https://dotnet.microsoft.com/download)
* [Install Docker](https://docs.docker.com/install/)

## Changes made to UI and API applications

* health check separated from api/weather calls:
   - UI
  ```cs
  app.UseEndpoints(endpoints =>
  {
      endpoints.MapGet("/", async context =>
      {
          await context.Response.WriteAsync("healthy");
      });

      endpoints.MapGet("/api", async context =>
      {
          var apiAddress = Environment.GetEnvironmentVariable("ApiAddress");
          var apiPort = Environment.GetEnvironmentVariable("ApiPort");
          var apiMethod = Environment.GetEnvironmentVariable("ApiMethod");
          await context.Response.WriteAsync($"Api Connection: {apiAddress}:{apiPort}/{apiMethod}");
      });
      
      endpoints.MapGet("/weather", async context =>
      {
          var apiAddress = Environment.GetEnvironmentVariable("ApiAddress") ?? "n/a";
          var apiPort = Environment.GetEnvironmentVariable("ApiPort") ?? "n/a";
          var apiMethod = Environment.GetEnvironmentVariable("ApiMethod") ?? "n/a";
          Console.WriteLine($"Api Connection: {apiAddress}:{apiPort}/{apiMethod}");
          if (string.IsNullOrEmpty(apiAddress) || string.IsNullOrEmpty(apiAddress) || string.IsNullOrEmpty(apiAddress))
          {
            logger.LogError($"Cannot connect to: {apiAddress}:{apiPort}/{apiMethod}");
            await context.Response.WriteAsync($"Empty address error: {apiAddress}:{apiPort}/{apiMethod}");
            return;
          }
          using var hc = new HttpClient();
          try
          {
            logger.LogInformation($"Trying connect to: {apiAddress}:{apiPort}/{apiMethod}");
            using var apiResponse = await hc.GetAsync($"{apiAddress}:{apiPort}/{apiMethod}");
            var apiResult = await apiResponse.Content.ReadAsStringAsync();
            await context.Response.WriteAsync(apiResult);
          }
          catch (Exception ex)
          {
            logger.LogError($"Cannot connect to: {apiAddress}:{apiPort}/{apiMethod}");
            await context.Response.WriteAsync($"Error connecting to: {apiAddress}:{apiPort}/{apiMethod}<br>{ex.ToString()}");
          }
      });
    ```
  - API
  ```cs
  app.UseEndpoints(endpoints =>
  {
    endpoints.MapGet("/", async context =>
    {
      await context.Response.WriteAsync("infa-api is running!");
    });
    endpoints.MapControllers();
  });
  ```
 

## Running this test application

Clone this repo and `cd` into it.

Next, to deploy the application and its infrastructure, follow these steps:

1. Create a new stack, which is an isolated deployment target for this example:

    ```bash
    $ pulumi stack init dev
    ```

2. Set your desired AWS region:

    ```bash
    $ pulumi config set aws:region us-east-2 # any valid AWS region will work
    ```

3. Deploy everything with a single `pulumi up` command. This will show you a preview of changes first, which
   includes all of the required AWS resources (clusters, services, and the like). Don't worry if it's more than
   you expected -- this is one of the benefits of Pulumi, it configures everything so that so you don't need to!

    ```bash
    $ pulumi up
    ```

    After being prompted and selecting "yes", your deployment will begin. It'll complete in a few minutes:

    ```
        Type                             Name                    Status              Info
    +   pulumi:pulumi:Stack              aws-pulumi-dev          created (143s)      
    +   ├─ aws:ecs:Cluster               infra-app-cluster       created (12s)       
    +   ├─ aws:iam:Role                  task-exec-role          created (4s)        
    +   ├─ aws:iam:RolePolicyAttachment  task-exec-policy        created (0.00s)     
    +   ├─ aws:lb:TargetGroup            infra-web-tg            created (0.00s)     
    +   ├─ aws:ec2:SecurityGroup         infra-web-sg            created (4s)        
    +   ├─ aws:ec2:SecurityGroup         infra-api-sg            created (0.00s)     
    +   ├─ aws:lb:LoadBalancer           infra-api-loadbalancer  created (131s)      
    +   ├─ aws:ecr:Repository            infra-api-repo          created (0.00s)     
    +   ├─ aws:lb:Listener               infra-api-listener      created (0.00s)     
    +   ├─ aws:ec2:DefaultVpc            default                 created (4s)        
    +   ├─ aws:lb:LoadBalancer           infra-web-loadbalancer  created (132s)      
    +   ├─ aws:ecs:TaskDefinition        infra-api-task          created (0.00s)     
    +   ├─ aws:ecs:TaskDefinition        infra-web-task          created (0.00s)     
    +   ├─ aws:ecs:Service               infra-web-svc           created (0.00s)     
    +   ├─ aws:ecs:Service               infra-api-svc           created (0.00s)     
    +   ├─ aws:ecr:Repository            infra-web-repo          created (0.00s)     
    +   ├─ aws:lb:Listener               infra-web-listener      created (0.00s)     
    +   ├─ docker:index:Image            infra-api-image         created (72s)       1 message
    +   ├─ docker:index:Image            infra-web-image         created (80s)       1 message
    +   └─ aws:lb:TargetGroup            infra-api-tg            created (0.00s)     
    
    Diagnostics:
      docker:index:Image (infra-api-image):
        Building your image for linux/amd64 architecture.
        To ensure you are building for the correct platform, consider explicitly setting the `platform` field on ImageBuildOptions.
    
      docker:index:Image (infra-web-image):
        Building your image for linux/amd64 architecture.
        To ensure you are building for the correct platform, consider explicitly setting the `platform` field on ImageBuildOptions.
    
    Outputs:
        ApiUrl: "http://internal-infra-api-loadbalancer-4079995-747140744.us-east-2.elb.amazonaws.com"
        Url   : "http://infra-web-loadbalancer-b663c0a-1094122919.us-east-2.elb.amazonaws.com"
        
    Resources:
        + 21 created
    ```

   Notice that the automatically assigned load-balancer URLs are printed as a stack output.

4. At this point, your app is running -- let's curl it. The CLI makes it easy to grab the URL:

    ```bash
    $ curl $(pulumi stack output url)
   healthy
    ```

6. Once you are done, you can destroy all of the resources, and the stack:

    ```bash
    $ pulumi destroy
    $ pulumi stack rm
    ```

## Final notes

> To provide tags for all resources that can accept tags I tried to use `ResourceTransformations` as per sample there `https://github.com/joeduffy/aws-tags-example.git`, but pulumi can not handle it (became frozen).
>
> Possibly due to a lack of experience.
>

> 