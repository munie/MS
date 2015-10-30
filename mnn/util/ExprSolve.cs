using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace mnn.util {
    public class ExprSolve {
        static private int check_expr(string expr)
        {
            // 有英文字符，则非法
            for (int i = 0; i < expr.Length; ++i) {
                if (char.IsLetter(expr[i]))
                    return -1;
            }

            // 首位不能是除 数字、（、+、- 以外的字符
            if (char.IsDigit(expr[0]) == false &&
                    expr[0] != '(' &&
                    expr[0] != '+' &&
                    expr[0] != '-')
                return -1;

            // 末位不能是除 数字、）、以外的字符
            if (char.IsDigit(expr[expr.Length - 1]) == false && expr[expr.Length - 1] != ')')
                return -1;

            // 除首末位以外的字符验证
            for (int i = 1; i < expr.Length - 1; ++i) {
                //printf("expression:%c, length:%d\n", expr[i], i);
                // 数字前面不能是），数字后面不能是（
                if (char.IsDigit(expr[i])) {
                    if (expr[i - 1] == ')')
                        return -1;
                    else
                        continue;
                }

                // . 前后必须都是数字
                if (expr[i] == '.') {
                    if (char.IsDigit(expr[i - 1]) && char.IsDigit(expr[i + 1]))
                        continue;
                    else
                        return -1;
                }

                // （ 前面不能是 数字、）
                if (expr[i] == '(') {
                    if (char.IsDigit(expr[i - 1]) || expr[i - 1] == ')')
                        return -1;
                    else
                        continue;
                }

                // ）前面不能是 （、+、-、*、/、%、^，或者说其前面必须是 数字、）
                if (expr[i] == ')') {
                    if (char.IsDigit(expr[i - 1]) || expr[i - 1] == ')')
                        continue;
                    else
                        return -1;
                }

                // 加减乘除模指 前面不能是 自身、（
                if (expr[i] == '+' || expr[i] == '-') {
                    if (expr[i - 1] == '+' || expr[i - 1] == '-' ||
                        expr[i - 1] == '*' || expr[i - 1] == '/' ||
                        expr[i - 1] == '%' || expr[i - 1] == '^')
                        return -1;
                    else
                        continue;
                }
                if (expr[i] == '*' || expr[i] == '/' ||
                    expr[i] == '%' || expr[i] == '^') {
                    if (expr[i - 1] == '+' || expr[i - 1] == '-' ||
                        expr[i - 1] == '*' || expr[i - 1] == '/' ||
                        expr[i - 1] == '%' || expr[i - 1] == '^' ||
                        expr[i - 1] == '(')
                        return -1;
                    else
                        continue;
                }
            }

            return 0;
        }

        static private int level(char oper)
        {
            int retValue = 0;

            switch (oper) {
                case '+':
                case '-':
                    retValue = 1;
                    break;

                case '*':
                case '/':
                case '%':
                    retValue = 2;
                    break;

                case '^':
                    retValue = 3;
                    break;
            }

            return retValue;
        }

        static private int preorder_to_postorder_expr(out StringBuilder result, string expr)
        {
            result = new StringBuilder();

            // 删除所有空白符，空白符两边都是数字，将会连一起，目前没法解决
            expr = expr.Replace(" ", "");
            expr = expr.Replace("\t", "");

            if (check_expr(expr) != 0) {
                //printf("表达式非法！\n");
                result.Append("表达式非法: " + expr);
                return -1;
            }

            //int location = 0;
            Stack<char> stack = new Stack<char>();

            stack.Push('#');

            for (int i = 0; i < expr.Length + 1; ++i) {
                if (i == expr.Length) {
                    while (stack.Peek() != '#') {
                        char tmp = stack.Pop();
                        //result[location++] = tmp;
                        //result[location++] = ' ';
                        result.Append(tmp);
                        result.Append(' ');
                    }
                    break;
                }

                if (char.IsDigit(expr[i]) || expr[i] == '.') {
                    //result[location++] = expr[i];
                    result.Append(expr[i]);
                    if (i == expr.Length - 1 || char.IsDigit(expr[i + 1]) == false && expr[i + 1] != '.')
                        //result[location++] = ' ';
                        result.Append(' ');

                } else if (expr[i] == '(') {
                    stack.Push(expr[i]);
                } else if (expr[i] == ')') {
                    while (stack.Peek() != '(') {
                        if (stack.Peek() == '#')
                            return -1;
                        char tmp = stack.Pop();
                        //result[location++] = tmp;
                        //result[location++] = ' ';
                        result.Append(tmp);
                        result.Append(' ');
                    }
                    stack.Pop();
                } else if (expr[i] == '+' || expr[i] == '-'
                          || expr[i] == '*' || expr[i] == '/'
                          || expr[i] == '^' || expr[i] == '%') {
                    if (expr[i] == '+' || expr[i] == '-') {
                        if (i == 0 || expr[i - 1] == '(') {
                            //result[location++] = '0';
                            //result[location++] = ' ';
                            result.Append('0');
                            result.Append(' ');
                        }
                    }
                    while (stack.Peek() != '#' && stack.Peek() != '('
                            && (level(stack.Peek()) - level(expr[i]) >= 0)) {
                        char tmp = stack.Pop();
                        //result[location++] = tmp;
                        //result[location++] = ' ';
                        result.Append(tmp);
                        result.Append(' ');
                    }
                    stack.Push(expr[i]);
                }
            }

            //result[location++] = '#';
            //result[location] = '\0';

            return 0;
        }

        static private double calculate_postorder_expr(string expr)
        {
            double retValue = 0;

            Stack<double> stack = new Stack<double>();

            StringBuilder tmp = new StringBuilder();
            //int location = 0;

            for (int i = 0; i < expr.Length; ++i) {
                if (char.IsDigit(expr[i]) || expr[i] == '.') {
                    //tmp[location++] = expr[i];
                    tmp.Append(expr[i]);

                    if (expr[i + 1] == ' ') {
                        //tmp[location] = '\0';
                        double operand = Convert.ToDouble(tmp.ToString());
                        stack.Push(operand);
                        //location = 0;
                        tmp.Clear();
                    }
                } else if (expr[i] != ' ') {
                    double operand_left = stack.Pop();

                    double result = 0;
                    switch (expr[i]) {
                        case '+':
                            result = stack.Pop() + operand_left;
                            break;
                        case '-':
                            result = stack.Pop() - operand_left;
                            break;
                        case '*':
                            result = stack.Pop() * operand_left;
                            break;
                        case '/':
                            result = stack.Pop() / operand_left;
                            break;
                        case '^':
                            result = Math.Pow(stack.Pop(), operand_left);
                            break;
                        case '%':
                            result = stack.Pop() % operand_left;
                            break;
                    }

                    stack.Push(result);
                }
            }

            retValue = stack.Pop();

            return retValue;
        }

        //static public double CalculateExpr(string expr)
        //{
        //    if (expr.Contains("new Random().NextDouble()"))
        //        expr = expr.Replace("new Random().NextDouble()", "(" + Math.Round(new Random().NextDouble(),2).ToString() + ")");
        //    else if (expr.Contains("new Random().Next()"))
        //        expr = expr.Replace("new Random().Next()", "(" + new Random().Next().ToString() + ")");

        //    StringBuilder postOrderExpr = new StringBuilder();

        //    if (ExprSolve.preorder_to_postorder_expr(out postOrderExpr, expr) != 0)
        //        throw new ApplicationException(postOrderExpr.ToString());

        //    return Math.Round(ExprSolve.calculate_postorder_expr(postOrderExpr.ToString()), 2);
        //}

        static public double CalculateExpr(string expr, int precision = 2, string replace = "", double value = 0)
        {
            if (expr.Contains("new Random().NextDouble()"))
                expr = expr.Replace("new Random().NextDouble()", "(" + Math.Round(new Random().NextDouble(), 2).ToString() + ")");
            else if (expr.Contains("new Random().Next()"))
                expr = expr.Replace("new Random().Next()", "(" + new Random().Next().ToString() + ")");

            if (!string.IsNullOrEmpty(replace))
                expr = expr.Replace(replace, "(" + value + ")");

            StringBuilder postOrderExpr = new StringBuilder();

            if (ExprSolve.preorder_to_postorder_expr(out postOrderExpr, expr) != 0)
                throw new ApplicationException(postOrderExpr.ToString());

            return Math.Round(ExprSolve.calculate_postorder_expr(postOrderExpr.ToString()), precision);
        }

        static public decimal CalculateExprDecimal(string expr, int precision = 2, string replace = "", decimal value = 0)
        {
            return (decimal)CalculateExpr(expr, precision, replace, (double)value);
        }
    }
}
