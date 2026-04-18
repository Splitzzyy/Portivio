import { z } from 'zod';

export const loginSchema = z.object({
  email: z.string().email('Enter a valid email'),
  password: z.string().min(8, 'At least 8 characters'),
});
export type LoginForm = z.infer<typeof loginSchema>;

export const signupSchema = z
  .object({
    name: z.string().min(2, 'Name too short'),
    email: z.string().email('Enter a valid email'),
    password: z
      .string()
      .min(8, 'At least 8 characters')
      .regex(/[A-Z]/, 'Need an uppercase letter')
      .regex(/[a-z]/, 'Need a lowercase letter')
      .regex(/[0-9]/, 'Need a digit'),
    confirmPassword: z.string(),
  })
  .refine((d) => d.password === d.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });
export type SignupForm = z.infer<typeof signupSchema>;

export const forgotSchema = z.object({
  email: z.string().email('Enter a valid email'),
});
export type ForgotForm = z.infer<typeof forgotSchema>;

export const resetSchema = z
  .object({
    email: z.string().email('Enter a valid email'),
    resetToken: z.string().min(1, 'Token required'),
    newPassword: z.string().min(8, 'At least 8 characters'),
    confirmPassword: z.string(),
  })
  .refine((d) => d.newPassword === d.confirmPassword, {
    message: 'Passwords do not match',
    path: ['confirmPassword'],
  });
export type ResetForm = z.infer<typeof resetSchema>;

export const verifySchema = z.object({
  email: z.string().email('Enter a valid email'),
  verificationToken: z.string().min(1, 'Token required'),
});
export type VerifyForm = z.infer<typeof verifySchema>;
