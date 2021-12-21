terraform {
  required_version = ">= 0.14"
  backend "s3" {
    bucket = "phonebook-tf-state"
    key    = "state.tfstate"
    region = "us-east-2"
  }
}

provider "aws" {
  region = "us-east-2"
}

# VPC

resource "aws_vpc" "vpc" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_support   = true
  enable_dns_hostnames = true
  tags = {
    Name = "Terraform VPC"
  }
}

# Internet gateway

resource "aws_internet_gateway" "internet_gateway" {
  vpc_id = aws_vpc.vpc.id
}

# Subnet

resource "aws_subnet" "pub_subnet_us1" {
  vpc_id     = aws_vpc.vpc.id
  cidr_block = "10.0.0.0/24"
  availability_zone = "us-east-2a"
}

resource "aws_subnet" "pub_subnet_us2" {
  vpc_id     = aws_vpc.vpc.id
  cidr_block = "10.0.1.0/24"
  availability_zone = "us-east-2b"
}

# Routing

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.vpc.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.internet_gateway.id
  }
}

resource "aws_route_table_association" "route_table_association_us1" {
  subnet_id      = aws_subnet.pub_subnet_us1.id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "route_table_association_us2" {
  subnet_id      = aws_subnet.pub_subnet_us2.id
  route_table_id = aws_route_table.public.id
}

# Security groups

resource "aws_security_group" "front_sg" {
  vpc_id = aws_vpc.vpc.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 65535
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "back_sg" {
  vpc_id = aws_vpc.vpc.id

  egress {
    from_port   = 0
    to_port     = 65535
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "rds_sg" {
  vpc_id = aws_vpc.vpc.id

  ingress {
    protocol    = "tcp"
    from_port   = 3306
    to_port     = 3306
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 65535
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

# Autoscaling groups

data "aws_iam_policy_document" "ecs_agent" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ec2.amazonaws.com"]
    }
  }
}

resource "aws_iam_role" "ecs_agent" {
  name               = "ecs-agent"
  assume_role_policy = data.aws_iam_policy_document.ecs_agent.json
}


resource "aws_iam_role_policy_attachment" "ecs_agent" {
  role       = aws_iam_role.ecs_agent.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonEC2ContainerServiceforEC2Role"
}

resource "aws_iam_instance_profile" "ecs_agent" {
  name = "ecs-agent"
  role = aws_iam_role.ecs_agent.name
}

resource "aws_launch_configuration" "ecs_launch_config" {
  image_id             = "ami-0fb653ca2d3203ac1"
  iam_instance_profile = aws_iam_instance_profile.ecs_agent.name
  security_groups      = [aws_security_group.back_sg.id]
  user_data            = "#!/bin/bash\necho ECS_CLUSTER=my-cluster >> /etc/ecs/ecs.config"
  instance_type        = "t2.micro"
}

resource "aws_autoscaling_group" "failure_analysis_ecs_asg" {
  name                 = "asg"
  vpc_zone_identifier  = [aws_subnet.pub_subnet_us1.id, aws_subnet.pub_subnet_us2.id]
  launch_configuration = aws_launch_configuration.ecs_launch_config.name

  desired_capacity          = 2
  min_size                  = 1
  max_size                  = 10
  health_check_grace_period = 300
  health_check_type         = "EC2"
}

# DB

resource "aws_db_subnet_group" "db_subnet_group" {
  subnet_ids = [aws_subnet.pub_subnet_us1.id, aws_subnet.pub_subnet_us2.id]
}

resource "aws_db_instance" "mysql" {
  identifier                = "mysql"
  allocated_storage         = 5
  backup_retention_period   = 2
  backup_window             = "01:00-01:30"
  maintenance_window        = "sun:03:00-sun:03:30"
  multi_az                  = true
  engine                    = "mysql"
  engine_version            = "5.7"
  instance_class            = "db.t2.micro"
  name                      = "phonebook_db"
  username                  = "db_user"
  password                  = "Pas$w0rd123!"
  port                      = "3306"
  db_subnet_group_name      = aws_db_subnet_group.db_subnet_group.id
  vpc_security_group_ids    = [aws_security_group.rds_sg.id, aws_security_group.back_sg.id]
  skip_final_snapshot       = true
  final_snapshot_identifier = "phonebook-final"
  publicly_accessible       = true
}

# ECS

resource "aws_ecr_repository" "front" {
    name  = "front"
}

resource "aws_ecr_repository" "back" {
    name  = "back"
}

resource "aws_ecs_cluster" "ecs_cluster" {
  name = "phonebook-cluster"
}

resource "aws_ecs_task_definition" "front_task_definition" {
  family = "phonebook"
  container_definitions = jsonencode([
    {
      "essential" : true,
      "memory" : 512,
      "name" : "front",
      "cpu" : 1,
      "image" : "${var.ecr_registry}/${var.front_image_name}:latest",
      "environment" : []
    }
  ])
}

resource "aws_ecs_task_definition" "back_task_definition" {
  family = "phonebook"
  container_definitions = jsonencode([
    {
      "essential" : true,
      "memory" : 512,
      "name" : "back",
      "cpu" : 1,
      "image" : "${var.ecr_registry}/${var.back_image_name}:latest",
      "environment" : []
    }
  ])
}

resource "aws_ecs_service" "front_service" {
  name            = "front"
  cluster         = aws_ecs_cluster.ecs_cluster.id
  task_definition = aws_ecs_task_definition.front_task_definition.arn
  desired_count   = 2
}

resource "aws_ecs_service" "back_service" {
  name            = "back"
  cluster         = aws_ecs_cluster.ecs_cluster.id
  task_definition = aws_ecs_task_definition.back_task_definition.arn
  desired_count   = 2
}

output "mysql_endpoint" {
  value = aws_db_instance.mysql.endpoint
}
