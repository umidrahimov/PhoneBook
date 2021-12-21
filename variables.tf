variable ecr_registry {
  type        = string
  default     = "761052549372.dkr.ecr.us-east-2.amazonaws.com/phonebook"
}

variable front_image_name {
  type        = string
  default     = "front"
}

variable back_image_name {
  type        = string
  default     = "back"
}
