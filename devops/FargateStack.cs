using System;
using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using Pulumi;
using Docker = Pulumi.Docker;
using Ec2 = Pulumi.Aws.Ec2;
using Ecs = Pulumi.Aws.Ecs;
using Ecr = Pulumi.Aws.Ecr;
using Elb = Pulumi.Aws.LB;
using Iam = Pulumi.Aws.Iam;
using Awsx = Pulumi.Awsx;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Pulumi.Aws.Ecs;

class FargateStack : Stack
{
  //[Output] public Output<ImmutableArray<string>> PrivateSubnetIds { get; private set; }
  //[Output] public Output<ImmutableArray<string>> PublicSubnetIds { get; private set; }
  //[Output] public Output<string> VpcId { get; set; }

  public FargateStack()
  {
    var webSrcFolder = "../infra-web";
    var apiSrcFolder = "../infra-api";
    
    //var customVpc = new Awsx.Ec2.Vpc("custom");

    //this.VpcId = customVpc.VpcId;
    //this.PublicSubnetIds = customVpc.PublicSubnetIds;
    //this.PrivateSubnetIds = customVpc.PrivateSubnetIds;

    // Read back the default VPC and public subnets, which we will use.
    var vpc = new Ec2.DefaultVpc("default", new()
    {
      Tags =
        {
            { "Name", "Default VPC" },
        },
    });
    var vpcId = vpc.Id;
    var vpcCidr = vpc.CidrBlock;
    var vpcMainRT = vpc.MainRouteTableId;
    var subnets = Ec2.GetSubnets.Invoke(new Ec2.GetSubnetsInvokeArgs
    {
      Filters = new[]
      {
        new Ec2.Inputs.GetSubnetsFilterInputArgs
        {
            Name = "vpc-id",
            Values = new[] { vpcId}
        }
      }
    });

    //var vpcId = Ec2.GetVpc.Invoke(new Ec2.GetVpcInvokeArgs { Default = true }).Apply(vpc => vpc.Id);
    //var vpcCidr = Ec2.GetVpc.Invoke(new Ec2.GetVpcInvokeArgs { Default = true }).Apply(vpc => vpc.CidrBlock);
    //var vpcMainRT = Ec2.GetVpc.Invoke(new Ec2.GetVpcInvokeArgs { Default = true }).Apply(vpc => vpc.MainRouteTableId);
    //var subnets = Ec2.GetSubnets.Invoke(new Ec2.GetSubnetsInvokeArgs
    //{
    //  Filters = new[]
    //  {
    //    new Ec2.Inputs.GetSubnetsFilterInputArgs
    //    {
    //        Name = "vpc-id",
    //        Values = new[] { vpcId}
    //    }
    //  }
    //});

    var subnetIds = subnets.Apply(s => s.Ids);

    var webSg = new Ec2.SecurityGroup("infra-web-sg", new Ec2.SecurityGroupArgs
    {
      VpcId = vpcId,
      Egress =
      {
        new Ec2.Inputs.SecurityGroupEgressArgs
        {
            Protocol = "-1",
            FromPort = 0,
            ToPort = 0,
            CidrBlocks = {"0.0.0.0/0"}
        }
      },
      Ingress =
      {
        new Ec2.Inputs.SecurityGroupIngressArgs
        {
            Protocol = "tcp",
            FromPort = 80,
            ToPort = 80,
            CidrBlocks =  {"0.0.0.0/0"}
        }
      }
    });

    // Create a load balancer to listen for HTTP traffic on port 80.
    var webLb = new Elb.LoadBalancer("infra-web-loadbalancer", new Elb.LoadBalancerArgs
    {
      Subnets = subnetIds,
      SecurityGroups = { webSg.Id }
    });

    // Create a SecurityGroup that permits HTTP ingress and unrestricted egress.
    var apiSg = new Ec2.SecurityGroup("infra-api-sg", new Ec2.SecurityGroupArgs
    {
      VpcId = vpcId,
      Egress =
      {
        new Ec2.Inputs.SecurityGroupEgressArgs
        {
            Protocol = "-1",
            FromPort = 0,
            ToPort = 0,
            CidrBlocks = {"0.0.0.0/0"}
        }
      },
      Ingress =
      {
        new Ec2.Inputs.SecurityGroupIngressArgs
        {
            Protocol = "tcp",
            FromPort = 80,
            ToPort = 80,
            CidrBlocks = {"0.0.0.0/0"}
        }
      }
    });


    var rolePolicyJson = JsonSerializer.Serialize(new
    {
      Version = "2008-10-17",
      Statement = new[]
        {
                new
                {
                    Sid = "",
                    Effect = "Allow",
                    Principal = new
                    {
                        Service = "ecs-tasks.amazonaws.com"
                    },
                    Action = "sts:AssumeRole"
                }
            }
    });

    // Create an IAM role that can be used by our service's task.
    var taskExecRole = new Iam.Role("task-exec-role", new Iam.RoleArgs
    {
      AssumeRolePolicy = rolePolicyJson
    });

    var taskExecAttach = new Iam.RolePolicyAttachment("task-exec-policy", new Iam.RolePolicyAttachmentArgs
    {
      Role = taskExecRole.Name,
      PolicyArn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
    });

    // Create an ECS cluster to run a container-based service.
    var cluster = new Ecs.Cluster("infra-app-cluster");

    /*
    * API Service
    */
    // Create a load balancer to listen for HTTP traffic on port 80.
    var apiLb = new Elb.LoadBalancer("infra-api-loadbalancer", new Elb.LoadBalancerArgs
    {
      Subnets = subnetIds,
      Internal = true,
      SecurityGroups = { apiSg.Id },
    });

    var apiTg = new Elb.TargetGroup("infra-api-tg", new Elb.TargetGroupArgs
    {
      Port = 80,
      Protocol = "HTTP",
      TargetType = "ip",
      VpcId = vpcId
    });

    var apiListener = new Elb.Listener("infra-api-listener", new Elb.ListenerArgs
    {
      LoadBalancerArn = apiLb.Arn,
      Port = 80,
      DefaultActions =
        {
          new Elb.Inputs.ListenerDefaultActionArgs
          {
            Type = "forward",
            TargetGroupArn = apiTg.Arn,
          }
        }
    });

    // Create a private ECR registry and build and publish our app's container image to it.
    var apiRepo = new Ecr.Repository("infra-api-repo", new Ecr.RepositoryArgs
    {
      // ForceDelete = true,
    });

    var apiRegistryInfo = apiRepo.RegistryId.Apply(async (id) =>
    {
      var creds = await Ecr.GetCredentials.InvokeAsync(new Ecr.GetCredentialsArgs { RegistryId = id });
      var decodedData = Convert.FromBase64String(creds.AuthorizationToken);
      var decoded = ASCIIEncoding.ASCII.GetString(decodedData);

      var parts = decoded.Split(':');
      if (parts.Length != 2)
      {
        throw new Exception("Invalid credentials");
      }

      return new Docker.Inputs.RegistryArgs
      {
        Server = creds.ProxyEndpoint,
        Username = parts[0],
        Password = parts[1],
      };
    });

    // Build and publish the app image.
    var apiImage = new Docker.Image("infra-api-image", new Docker.ImageArgs
    {
      Build = new Pulumi.Docker.Inputs.DockerBuildArgs { Context = apiSrcFolder },
      ImageName = apiRepo.RepositoryUrl,
      Registry = apiRegistryInfo,
    });

    // Spin up a load balanced service running our container image.
    var apiTask = new Ecs.TaskDefinition("infra-api-task", new Ecs.TaskDefinitionArgs
    {
      Family = "fargate-task-definition",
      Cpu = "256",
      Memory = "512",
      NetworkMode = "awsvpc",
      RequiresCompatibilities = { "FARGATE" },
      ExecutionRoleArn = taskExecRole.Arn,
      ContainerDefinitions = apiImage.ImageName.Apply(imageName => JsonSerializer.Serialize(new[]
      {
        new
        {
          name = "infra-api",
          image = imageName,
          portMappings = new[]
          {
            new
            {
              containerPort = 80,
              hostPort = 80,
              protocol = "tcp"
            }
          },
          Environment = new[]
          {
            new {
              name = "ASPNETCORE_URLS",
              value = "http://+:80"
            }          
          }
        }
      })),
    });

    var apiSvc = new Ecs.Service("infra-api-svc", new Ecs.ServiceArgs
      {
        Cluster = cluster.Arn,
        DesiredCount = 1,
        LaunchType = "FARGATE",
        TaskDefinition = apiTask.Arn,
        NetworkConfiguration = new Ecs.Inputs.ServiceNetworkConfigurationArgs
        {
          AssignPublicIp = true,
          Subnets = subnetIds,
          SecurityGroups = { apiSg.Id }
        },
        LoadBalancers =
          {
            new Ecs.Inputs.ServiceLoadBalancerArgs
            {
              TargetGroupArn = apiTg.Arn,
              ContainerName = "infra-api",
              ContainerPort = 80
            }
          }
      }, 
      new CustomResourceOptions { DependsOn = { apiListener } }
    );

    // Export the resulting web address.
    ApiUrl = Output.Format($"http://{apiLb.DnsName}");

    /*
    * WEB application
    */

    var webTg = new Elb.TargetGroup("infra-web-tg", new Elb.TargetGroupArgs
    {
      Port = 80,
      Protocol = "HTTP",
      TargetType = "ip",
      VpcId = vpcId
    });

    var webListener = new Elb.Listener("infra-web-listener", new Elb.ListenerArgs
    {
      LoadBalancerArn = webLb.Arn,
      Port = 80,
      DefaultActions =
        {
          new Elb.Inputs.ListenerDefaultActionArgs
          {
            Type = "forward",
            TargetGroupArn = webTg.Arn,
          }
        }
    });

    // Create a private ECR registry and build and publish our app's container image to it.
    var webRepo = new Ecr.Repository("infra-web-repo", new Ecr.RepositoryArgs
    {
      //ForceDelete = true,
    });

    var webRegistryInfo = webRepo.RegistryId.Apply(async (id) =>
    {
      var creds = await Ecr.GetCredentials.InvokeAsync(new Ecr.GetCredentialsArgs { RegistryId = id });
      var decodedData = Convert.FromBase64String(creds.AuthorizationToken);
      var decoded = ASCIIEncoding.ASCII.GetString(decodedData);

      var parts = decoded.Split(':');
      if (parts.Length != 2)
      {
        throw new Exception("Invalid credentials");
      }

      return new Docker.Inputs.RegistryArgs
      {
        Server = creds.ProxyEndpoint,
        Username = parts[0],
        Password = parts[1],
      };
    });

    // Build and publish the app image.
    var webImage = new Docker.Image("infra-web-image", new Docker.ImageArgs
    {
      Build = new Pulumi.Docker.Inputs.DockerBuildArgs { Context = webSrcFolder },
      ImageName = webRepo.RepositoryUrl,
      Registry = webRegistryInfo,
    });

    // Spin up a load balanced service running our container image.
    var webTask = new Ecs.TaskDefinition("infra-web-task", new Ecs.TaskDefinitionArgs
    {
      Family = "fargate-task-definition",
      Cpu = "256",
      Memory = "512",
      NetworkMode = "awsvpc",
      RequiresCompatibilities = { "FARGATE" },
      ExecutionRoleArn = taskExecRole.Arn,
      ContainerDefinitions = apiLb.DnsName.Apply(DnsName => webImage.ImageName.Apply(imageName => JsonSerializer.Serialize(new[]
      {
        new
        {
          name = "infra-web",
          image = imageName,
          portMappings = new[]
          {
            new
            {
              containerPort = 80,
              hostPort = 80,
              protocol = "tcp"
            }
          },
          Environment = new[]
          {
            new {
              name = "ApiAddress",
              value = $"http://{DnsName}"
            },
            new {
              name = "ApiPort",
              value = "80" //
            },
            new {
              name = "ApiMethod",
              value = "WeatherForecast" //
            },
            new {
              name = "ASPNETCORE_URLS",
              value = "http://+:80"
            }          

          }
        }
      }))),
    });

    var appSvc = new Ecs.Service("infra-web-svc", new Ecs.ServiceArgs
      {
        Cluster = cluster.Arn,
        DesiredCount = 1,
        LaunchType = "FARGATE",
        TaskDefinition = webTask.Arn,
        NetworkConfiguration = new Ecs.Inputs.ServiceNetworkConfigurationArgs
        {
          AssignPublicIp = true,
          Subnets = subnetIds,
          SecurityGroups = { webSg.Id }
        },
        LoadBalancers =
          {
            new Ecs.Inputs.ServiceLoadBalancerArgs
            {
              TargetGroupArn = webTg.Arn,
              ContainerName = "infra-web",
              ContainerPort = 80
            }
          }
      }, 
      new CustomResourceOptions { DependsOn = { webListener, apiLb } }
      );

    // Export the resulting web address.
    Url = Output.Format($"http://{webLb.DnsName}");
    VPC = vpcCidr;
  }

  [Output] public Output<string> Url { get; set; }
  [Output] public Output<string> ApiUrl { get; set; }
  [Output] public Output<string> VPC { get; set; }
}
